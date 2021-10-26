using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace revecs.Generator;

public record QuerySource
(
    INamedTypeSymbol? Parent,
    string? Namespace,
    string Name,
    string FilePath,
    INamedTypeSymbol[] Header,
    bool IsRecord = false,
    string? StructureName = null,
    // system usage
    string? ValidStructureName = null,
    bool AlreadyGenerated = false
)
{
    public string GetDisplayName() => ValidStructureName ?? Name;
}

public class QueryGenerator
{
    public const string OptionalType = "Optional";
    public const string SingletonType = "Singleton";
    
    public readonly GeneratorExecutionContext Context;
    public readonly SyntaxReceiver Receiver;

    public Compilation Compilation;
    
    public QueryGenerator(GeneratorExecutionContext context, SyntaxReceiver syntaxReceiver,
        ref Compilation compilation)
    {
        Context = context;
        Receiver = syntaxReceiver;

        Compilation = compilation;
        
        CreateQueries();
        GenerateHelpers();

        compilation = Compilation;
    }

    private HashSet<(string? ns, int count)> _queryCount = new();

    private void Log<T>(int indent, T txt)
    {
        Receiver.Log.Add(string.Join(null, Enumerable.Repeat("\t", indent)) + txt);
    }

    public void GenerateHelpers()
    {
        var helpersSource =
            $@"
using System;
using System.Runtime.CompilerServices;
namespace revecs
{{
    internal class QueryAttribute : Attribute {{ }}
    internal class SingletonAttribute : Attribute {{ }}

    internal interface IQuery<T> {{ }}

    /// <summary>Transform IQuery functions for singleton usage</summary>
    internal interface {SingletonType} {{ }}

    internal interface Write<T> {{ }}

    internal interface Read<T> {{ }}

    internal interface With<T> {{ }}

    internal interface None<T> {{ }}

    internal interface Or<T> {{ }}

}}
";
        // {string.Join("\n    ", AdditionalQueryTypes)}
        Context.AddSource("QueryAttributes", helpersSource);
        Compilation = Compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText
            (
                helpersSource,
                (Context.Compilation as CSharpCompilation)?.SyntaxTrees[0].Options as CSharpParseOptions
            )
        );
    }
    
    public INamedTypeSymbol GetSingletonTypeSymbol()
    {
        var type = Compilation.GetUnknownType("global::revecs.Singleton")
            ?? Compilation.GetUnknownType("global::revecs.SingletonAttribute");
        if (type == null)
            throw new InvalidOperationException("Singleton Type not found");

        return type;
    }
    
    private static IEnumerable<INamedTypeSymbol> GetParentTypes(INamedTypeSymbol? type)
    {
        while (type != null)
        {
            yield return type;
            type = type.ContainingType;
        }
    }

    public void GenerateQuery(QuerySource source)
    {
        var sb = new StringBuilder();
        sb.Append(@"using revecs.Core;
using revecs.Utility;
using revecs.Query;
using revecs.Systems;
using revtask.Core;
using revtask.Helpers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
");

        var isSingleton = source.Header.Any(t => t.Name is SingletonType or $"{SingletonType}Attribute");

        void BeginNamespace()
        {
            if (source.Namespace is { } ns)
                sb.AppendLine($"namespace {ns} {{");
        }

        void EndNamespace()
        {
            if (source.Namespace is { } ns)
                sb.AppendLine("}");
        }

        var parentTypes = GetParentTypes(source.Parent).Reverse().ToList();

        void BeginParentTypes()
        {
            foreach (var parentType in parentTypes)
            {
                static string GetKindStr(ITypeSymbol s)
                {
                    return s.TypeKind switch
                    {
                        TypeKind.Class => "class",
                        TypeKind.Struct => "struct",
                        _ => s.TypeKind.ToString().ToLower()
                    };
                }

                sb.Append("partial ").Append(GetKindStr(parentType)).Append(' ').AppendLine(parentType.Name);
                sb.AppendLine("{");
            }
        }

        void EndParentTypes()
        {
            foreach (var _ in parentTypes) sb.AppendLine("}");
        }

        var arguments = new List<QueryArgument>();
        var isHeaderlessQuery = true;
        
        void CreateArguments()
        {
            foreach (var arg in source.Header)
            {
                var queryArgument = new QueryArgument
                {
                    Symbol = arg,
                    IsOption = true
                };

                switch (arg.Name)
                {
                    case "Write":
                        isHeaderlessQuery = false;

                        queryArgument.CanWrite = true;
                        queryArgument.IsOption = false;
                        queryArgument.Symbol = (INamedTypeSymbol) arg.TypeArguments[0];
                        queryArgument.Modifier = QueryArgument.EModifier.All;
                        // Custom after we set symbol
                        queryArgument.Custom = ComponentGenerator.GetCustomAccess(Compilation, queryArgument.Symbol);

                        break;
                    case "Read":
                        isHeaderlessQuery = false;
                        
                        queryArgument.IsOption = false;
                        queryArgument.Symbol = (INamedTypeSymbol) arg.TypeArguments[0];
                        queryArgument.Modifier = QueryArgument.EModifier.All;
                        // Custom after we set symbol
                        queryArgument.Custom = ComponentGenerator.GetCustomAccess(Compilation, queryArgument.Symbol);
                        break;

                    case "With":
                        queryArgument.IsOption = true;
                        queryArgument.Modifier = QueryArgument.EModifier.All;
                        queryArgument.Symbol = (INamedTypeSymbol) arg.TypeArguments[0];
                        break;
                    case "None":
                        queryArgument.IsOption = true;
                        queryArgument.Modifier = QueryArgument.EModifier.None;
                        queryArgument.Symbol = (INamedTypeSymbol) arg.TypeArguments[0];
                        break;
                    case "Or":
                        queryArgument.IsOption = true;
                        queryArgument.Modifier = QueryArgument.EModifier.Or;
                        queryArgument.Symbol = (INamedTypeSymbol) arg.TypeArguments[0];
                        break;
                }

                arguments.Add(queryArgument);
            }

            foreach (var a in arguments)
            {
                Log(4, $"{a.Symbol.Name}, ReadOnly: {!a.CanWrite}, Modifier: {a.Modifier}");
                if (a.Custom is {Usings: { } imports})
                {
                    sb.Insert(0, imports);
                }
            }
        }

        void BeginStruct()
        {
            var structName = source.StructureName ?? source.Name;
            
            sb.Append("    ");
            sb.AppendFormat("partial {1} struct {0}\n    {{\n", structName, source.IsRecord ? "record" : string.Empty);
        }

        void EndStruct()
        {
            sb.AppendLine("    }");
        }

        void Fields()
        {
            sb.AppendLine("        public readonly ArchetypeQuery Query;");
            sb.AppendLine("        public readonly SwapDependency EntityDependency;");

            foreach (var arg in arguments)
            {
                if (arg.IsOption)
                    continue;

                var custom = ComponentGenerator.GetCustomAccess(Compilation, arg.Symbol);

                sb.AppendLine();
                sb.AppendLine(
                    $"        private ComponentType<{arg.Symbol.GetTypeName()}> {arg.Symbol.Name}Type;"
                );
                sb.AppendLine(
                    $"        private SwapDependency {arg.Symbol.Name}Dependency;"
                );
            }
        }

        void Constructor()
        {
            var expr = new StringBuilder();
            foreach (var arg in arguments)
            {
                if (arg.IsOption)
                    continue;

                expr.Append("            ");
                expr.AppendLine(
                    $"{arg.Symbol.Name}Type = {arg.Symbol.GetTypeName()}.Type.GetOrCreate(world);"
                );
                expr.Append("            ");
                expr.AppendLine(
                    $"{arg.Symbol.Name}Dependency = world.GetComponentDependency({arg.Symbol.Name}Type);"
                );
            }
            
            var constructorAll = new List<string>();
            var constructorOr = new List<string>();
            var constructorNone = new List<string>();
            foreach (var arg in arguments)
            {
                if (arg.Modifier == QueryArgument.EModifier.Unknown)
                    continue;
                
                var getType = $"{arg.Symbol.GetTypeName()}.Type.GetOrCreate(world)";
                switch (arg.Modifier)
                {
                    case QueryArgument.EModifier.All:
                        constructorAll.Add(getType);
                        break;
                    case QueryArgument.EModifier.None:
                        constructorNone.Add(getType);
                        break;
                    case QueryArgument.EModifier.Or:
                        constructorOr.Add(getType);
                        break;
                }
            }

            sb.Append($@"
        public {source.Name}(RevolutionWorld world)
        {{
            Query = new ArchetypeQuery(world
                , all: new ComponentType[] 
                {{ 
                    {string.Join(",\n                    ", constructorAll)} 
                }} 
                , none: new ComponentType[] 
                {{ 
                    {string.Join(",                    ", constructorNone)}    
                }} 
                , or: new ComponentType[] 
                {{ 
                    {string.Join(",                    ", constructorOr)} 
                }} 
            );

            EntityDependency = world.GetEntityDependency();    

{expr}        
        }}
");
        }

        void Dependency()
        {
            // for now assume we only read entities (if there is a ICmdEntityAdmin it will swap the dependency without issues)
            // later, if there will be a IQueryRemoveAllEntities option, then modify that field
            var writeEntity = false;
            
            var dependencies = new StringBuilder();
            if (writeEntity)
            {
                dependencies.AppendLine("EntityDependency.TrySwap(runner, request)");
            }
            else
            {
                dependencies.AppendLine("EntityDependency.IsCompleted(runner, request)");
            }

            var reader = new StringBuilder();
            if (!writeEntity)
                reader.AppendLine("            EntityDependency.AddReader(request);");

            foreach (var arg in arguments)
            {
                if (arg.IsOption)
                    continue;

                dependencies.Append("               ");

                if (arg.CanWrite)
                {
                    dependencies.AppendLine($"&& {arg.Symbol.Name}Dependency.TrySwap(runner, request)");
                }
                else
                {
                    dependencies.AppendLine($"&& {arg.Symbol.Name}Dependency.IsCompleted(runner, request)");
                    reader.AppendLine($"            {arg.Symbol.Name}Dependency.AddReader(request);");
                }
            }

            sb.Append($@"
        public bool DependencySwap(IJobRunner runner, JobRequest request)
        {{
            return {dependencies};
        }}

        public void AddDependencyReader(IJobRunner runner, JobRequest request)
        {{
{reader}
        }}
");
        }
        
        void ImplementHeaderlessQuery()
        {
            sb.Append($@"
        
        public ArchetypeQueryEnumerator GetEnumerator() => Query.GetEnumerator();
");
        }
        
        void ImplementSingleton()
        {
            sb.AppendLine(@"
        public UEntityHandle Entity { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Query.Any() ? Query.EntityAt(0) : default; }");
            
            
            var argumentCount = arguments.Count(a => !a.IsOption);
            // UEntityHandle Entity;
            if (argumentCount == 0)
            {
                // TODO: implement
                ZeroArg();
            }
            // Only one argument, so flatten and put all the fields onto the query itself.
            // (With a way to retrieve the full component as the third option)
            //
            // UEntityHandle Entity;
            // (same as how a R* component work)
            // ComponentVariable1 Variable1;
            // ComponentVariable2 Variable2;
            // ...
            else if (argumentCount == 1)
            {
                OneArg();
            }
            // More than one arguments, each type accessible via field.
            //
            // UEntityHandle Entity;
            // Component1 Component1;
            //      ComponentVariable1 Variable1;
            // Component2 Component2;
            //      ComponentVariable1 Variable1;
            // ...
            else
            {
                MultipleArg();
            }

            void ZeroArg()
            {
                // literally nothing
            }

            void OneArg()
            {
                // we do that since we will show the component and flattened fields
                MultipleArg();

                foreach (var arg in arguments)
                {
                    if (arg.IsOption)
                        continue;
                    
                    if (arg.Custom.DisableReferenceWrapper)
                        continue;

                    var fields = new StringBuilder();
                    foreach (var f in arg.Symbol.GetMembers())
                    {
                        var ro = arg.CanWrite ? string.Empty : " readonly";

                        switch (f)
                        {
                            case IFieldSymbol fieldSymbol:
                                // from property
                                if (fieldSymbol.IsImplicitlyDeclared)
                                {
                                    continue;
                                }

                                fields.Append($@"
        public ref{ro} {fieldSymbol.Type.GetTypeName()} {fieldSymbol.Name} 
        {{ 
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ref {arg.Symbol.Name}.{fieldSymbol.Name}; 
        }}
");
                                break;
                            case IPropertySymbol propertySymbol:
                                var attr = "[MethodImpl(MethodImplOptions.AggressiveInlining)]";

                                string get = "", set = "";
                                if (propertySymbol.GetMethod != null)
                                    get = $"{attr} get => {arg.Symbol.Name}.{propertySymbol.Name};";
                                if (propertySymbol.SetMethod != null || propertySymbol.ReturnsByRef)
                                    set = $"{attr} set => {arg.Symbol.Name}.{propertySymbol.Name} = value;";
                                
                                fields.Append($@"
        public {propertySymbol.Type.GetTypeName()} {propertySymbol.Name} 
        {{
            {get} 
            {set} 
        }}
");
                                break;
                        }
                    }

                    sb.AppendLine(fields.ToString());
                }
            }

            void MultipleArg()
            {
                foreach (var arg in arguments)
                {
                    if (arg.IsOption)
                        continue;

                    var access = "ref";
                    if (!arg.CanWrite)
                        access += " readonly";
                    
                    if (arg.Custom.ViaWorld is not { } viaWorld)
                    {
                        sb.AppendLine($@"
        public {access} {arg.Symbol.GetTypeName()} {arg.Symbol.Name}
        {{ 
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get
            {{
                 return ref (Query.Any() 
                        ? ref Query.World.GetComponentData(Query.EntityAt(0), {arg.Symbol.Name}Type)
                        : ref Unsafe.NullRef<{arg.Symbol.GetTypeName()}>()); 
            }}
        }}");
                    }
                    else
                    {
                        sb.AppendLine($@"
        public {access} {viaWorld.ValueType} {arg.Symbol.Name}
        {{ 
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get
            {{
                if (!Query.Any())
                    return ref Unsafe.NullRef<{viaWorld.ValueType}>();

                ref var {viaWorld.GetAccess("value", "Query.World", "Query.EntityAt(0)", $"{arg.Symbol.Name}Type", "ref")};
                return ref value;
            }}
        }}");
                    }
                }
            }
        }

        void ImplementForeach()
        {
            sb.Append($@"
        
        public Enumerator GetEnumerator() => new(this);
");

            var iterationInit = new StringBuilder();
            var accessorFields = new StringBuilder();
            var accessorInit = new StringBuilder();
            foreach (var arg in arguments)
            {
                if (arg.IsOption)
                    continue;
                
                if (arg.Modifier == QueryArgument.EModifier.None)
                    continue;

                if (arg.Custom is {ViaAccessor: { } accessor})
                {
                    accessorFields.Append("            ");
                    accessorFields.AppendLine(
                        $"public {accessor.FieldType} Accessor{arg.Symbol.Name};"
                    );

                    accessorInit.Append("                ");
                    accessorInit.AppendLine(
                        $"{accessor.GetInit($"Accessor{arg.Symbol.Name}", $"self.{arg.Symbol.Name}Type", "_world")}"
                    );

                    // If wrapper is disabled, then it's a copy
                    // ( pls C# team, introduce ByRef values :( )
                    var iterationAccess = arg.Custom.DisableReferenceWrapper
                        ? $"{arg.Symbol.Name}ValueRef"
                        : $"new R{arg.Symbol.Name} {{ Span = MemoryMarshal.CreateSpan(ref Accessor{arg.Symbol.Name}[iter.Handle], 1) }}";

                    iterationInit.Append($@"
                    ref var {accessor.GetAccess($"{arg.Symbol.Name}ValueRef", $"Accessor{arg.Symbol.Name}", "iter.Handle", "ref")};
                    iter.{arg.Symbol.Name} = {iterationAccess};
");
                }
                else
                {
                    // Use EntityComponentAccessor since it's the safest choice for accessing any components
                    accessorFields.Append("            ");
                    accessorFields.AppendLine(
                        $"public EntityComponentAccessor<{arg.Symbol.GetTypeName()}> Accessor{arg.Symbol.Name}; ");

                    accessorInit.Append("                ");
                    accessorInit.AppendLine(
                        $"Accessor{arg.Symbol.Name} = _world.AccessEntityComponent(self.{arg.Symbol.Name}Type); ");

                    iterationInit.Append("                    ");
                    iterationInit.AppendLine(
                        $"iter.{arg.Symbol.Name} = new R{arg.Symbol.Name} {{ Span = MemoryMarshal.CreateSpan(ref Accessor{arg.Symbol.Name}[iter.Handle], 1) }}; ");
                }
            }

            sb.Append($@"
        public ref struct Enumerator
        {{
            private RevolutionWorld _world;
            private ArchetypeQueryEnumerator _enumerator;

{accessorFields}

            public Iteration Current
            {{
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get {{
                    Iteration iter;
                    iter.Handle = _enumerator.Current;
{iterationInit}

                    return iter;
                }}
            }}

            public Enumerator({source.StructureName ?? source.Name} self)
            {{
                this = default;

                _world = self.Query.World;
                _enumerator = self.Query.GetEnumerator();

{accessorInit}
            }}

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {{
                return _enumerator.MoveNext();
            }}
        }}
");
            var iteration = new StringBuilder(@"
        public ref struct Iteration
        {
");
            var deconstructOut = new StringBuilder();
            var deconstruct = new List<string>();
            var iterationFields = new StringBuilder("            public UEntityHandle Handle;\n");
            foreach (var arg in arguments)
            {
                if (arg.IsOption || arg.Modifier == QueryArgument.EModifier.None)
                    continue;

                if (!(arg.Custom is {DisableReferenceWrapper: true}))
                {
                    var fields = new StringBuilder();
                    foreach (var f in arg.Symbol.GetMembers())
                    {
                        var ro = arg.CanWrite ? string.Empty : " readonly";

                        switch (f)
                        {
                            case IFieldSymbol fieldSymbol:
                                // from property
                                if (fieldSymbol.IsImplicitlyDeclared)
                                {
                                    continue;
                                }

                                fields.Append($@"
            public ref{ro} {fieldSymbol.Type.GetTypeName()} {fieldSymbol.Name} 
            {{ 
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ref __ref.{fieldSymbol.Name}; 
            }}
");
                                break;
                            case IPropertySymbol propertySymbol:
                                var attr = "[MethodImpl(MethodImplOptions.AggressiveInlining)]";

                                string get = "", set = "";
                                if (propertySymbol.GetMethod != null)
                                    get = $"{attr} get => __ref.{propertySymbol.Name};";
                                if (propertySymbol.SetMethod != null || propertySymbol.ReturnsByRef)
                                    set = $"{attr} set => __ref.{propertySymbol.Name} = value;";

                                fields.Append($@"
            public {propertySymbol.Type.GetTypeName()} {propertySymbol.Name} 
            {{
                {get} 
                {set} 
            }}
");
                                break;
                        }
                    }

                    sb.Append($@"
        public ref struct R{arg.Symbol.Name}
        {{
            public Span<{arg.Symbol.GetTypeName()}> Span;

            public ref {arg.Symbol.GetTypeName()} __ref {{ [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ref MemoryMarshal.GetReference(Span); }}

            {fields}

            public override string ToString() => __ref.ToString();
        }}
        ");
                    deconstruct.Add($"out R{arg.Symbol.Name} {arg.Symbol.Name}");
                    deconstructOut.Append($"                {arg.Symbol.Name} = this.{arg.Symbol.Name};\n");
                    iterationFields.AppendLine($"            public R{arg.Symbol.Name} {arg.Symbol.Name};");
                }
                else
                {
                    var typeName = arg.Symbol.GetTypeName();
                    if (arg.Custom is {ViaAccessor: { } viaAccessor})
                    {
                        typeName = (viaAccessor.ValueType ?? typeName);
                    }

                    deconstruct.Add($"out {typeName} {arg.Symbol.Name}");
                    deconstructOut.Append($"                {arg.Symbol.Name} = this.{arg.Symbol.Name};\n");
                    iterationFields.AppendLine($"            public {typeName} {arg.Symbol.Name};");
                }
            }

            iteration.Append($@"{iterationFields}

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Deconstruct(out UEntityHandle handle, {string.Join(',', deconstruct)})
            {{
                handle = this.Handle;
{deconstructOut}            }}

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Deconstruct({string.Join(',', deconstruct)})
            {{
{deconstructOut}            }}
        }}");
            sb.AppendLine(iteration.ToString());
        }

        void CreateInterface()
        {
            var validArgs = arguments
                .Where(a => !a.IsOption)
                .Select(a => $"T{a.Symbol.Name}")
                .ToArray();
            
            if (_queryCount.Contains((source.Namespace, validArgs.Length)))
                return;

            var interfaceSb = new StringBuilder();
            interfaceSb.AppendLine($"namespace {source.Namespace} {{");
            interfaceSb.Append($"    internal interface IQuery");
            if (validArgs.Any())
            {
                interfaceSb.Append('<');
                interfaceSb.Append(
                    string.Join(", ", validArgs)
                );
                interfaceSb.AppendLine(">");
            }

            interfaceSb.AppendLine("    {}");
            interfaceSb.AppendLine("}");
            
            _queryCount.Add((source.Namespace, validArgs.Length));
            
            var fileName = $"{source.Namespace ?? "undefined"}.QueryInterfaceL{validArgs.Length}";
            Context.AddSource($"{fileName}", "#pragma warning disable\n" + interfaceSb.ToString());
        }

        void AdditionalFunctions()
        {
            sb.AppendLine($@"
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Any()
        {{
            return Query.Any();
        }}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEntityCount()
        {{
            return Query.GetEntityCount();
        }}
");
        }

        BeginNamespace();
        {
            BeginParentTypes();
            {
                CreateArguments();
                BeginStruct();
                {
                    Fields();
                    Constructor();

                    if (isHeaderlessQuery)
                    {
                        ImplementHeaderlessQuery();
                    }
                    else
                    {
                        if (isSingleton)
                        {
                            ImplementSingleton();
                        }
                        else
                        {
                            ImplementForeach();
                        }
                    }
                    
                    Dependency();
                    AdditionalFunctions();
                }
                EndStruct();
            }
            EndParentTypes();

            CreateInterface();
        }
        EndNamespace();
        
        var fileName = $"{(source.Parent == null ? "" : $"{source.Parent.Name}.")}{source.Name}";
        Context.AddSource($"{Path.GetFileNameWithoutExtension(source.FilePath)}.{fileName}", sb.ToString());
    }

    public void CreateQueries()
    {
        foreach (var tree in Compilation.SyntaxTrees)
        {
            var semanticModel = Compilation.GetSemanticModel(tree);
            foreach (var declaredStruct in tree
                         .GetRoot()
                         .DescendantNodesAndSelf()
                         .OfType<TypeDeclarationSyntax>())
            {
                if (declaredStruct is InterfaceDeclarationSyntax)
                    continue;
                
                if (tree.FilePath.Contains("Generated/Generator"))
                    continue;
                
                var symbol = (INamedTypeSymbol) ModelExtensions.GetDeclaredSymbol(semanticModel, declaredStruct);

                // is query
                var queryInterface = symbol!.Interfaces.FirstOrDefault(i => i.Name == "IQuery");
                if (queryInterface != null)
                {
                    Log(0, "Found Query: " + symbol.GetTypeName() + ", File: " + tree.FilePath);
                    
                    var source = new QuerySource
                    (
                        symbol.ContainingType,
                        symbol.ContainingNamespace?.ToString(),
                        symbol.Name,
                        tree.FilePath,
                        queryInterface.TypeArguments.Select(t =>
                        {
                            Log(3, $"Found Argument {t.Name}");
                            return (INamedTypeSymbol) t;
                        }).Union(symbol.Interfaces.Where(inter =>
                        {
                            return !(SymbolEqualityComparer.Default.Equals(inter, queryInterface) || inter.Name == "IEquatable");
                        }), SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>().ToArray(),
                        IsRecord: symbol.IsRecord
                    );
                    
                    GenerateQuery(source);
                }
            }
        }
    }

    public struct QueryArgument
    {
        public enum EModifier
        {
            Unknown,
            All,
            None,
            Or
        }

        public INamedTypeSymbol Symbol;
        public bool CanWrite;
        public bool IsOption;
        public EModifier Modifier;

        public ComponentGenerator.CustomAccessResult Custom;
    }
}