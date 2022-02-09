using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace revecs.Generator;

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
    public string PublicName;
    
    public bool CanWrite;
    public bool IsOption;
    public EModifier Modifier;
    public ComponentGenerator.CustomAccessResult Custom;
}

public record QuerySource
(
    INamedTypeSymbol? Parent,
    string? Namespace,
    string Name,
    string FilePath,
    QueryArgument[] Arguments,
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
    internal interface IQuery<T> {{ }}

    internal interface Write<T> {{ }}

    internal interface Read<T> {{ }}

    internal interface All<T> {{ }}

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
using revecs.Querying;
using revecs.Systems;
using revtask.Core;
using revtask.Helpers;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.ComponentModel;

");

        foreach (var arg in source.Arguments)
        {
            if (arg.Custom is {Usings: { } imports})
            {
                sb.Append($"// Imports from {arg.Symbol.Name}");
                sb.AppendLine(imports);
                sb.AppendLine();
            }
        }
        
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
        
        var isHeaderlessQuery = source.Arguments.Any(c => !c.IsOption) == false;
        isHeaderlessQuery = false;

        void BeginStruct()
        {
            var structName = source.StructureName ?? source.Name;
            
            sb.Append("    ");
            if (structName.StartsWith("__"))
                sb.Append("[EditorBrowsable(EditorBrowsableState.Never)]");
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
            sb.AppendLine("        public readonly SwapDependency WorldDependency;");

            foreach (var arg in source.Arguments)
            {
                if (arg.IsOption)
                    continue;
                
                sb.AppendLine();
                sb.AppendLine(
                    $"        private ComponentType {arg.Symbol.Name}Type;"
                );
                sb.AppendLine(
                    $"        private SwapDependency {arg.Symbol.Name}Dependency;"
                );
            }
        }

        void Constructor()
        {
            var expr = new StringBuilder();
            foreach (var arg in source.Arguments)
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
            foreach (var arg in source.Arguments)
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
        public {source.StructureName ?? source.Name}(RevolutionWorld world)
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
            WorldDependency = world.GetWorldDependency();

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

            dependencies.AppendLine("&& WorldDependency.IsCompleted(runner, request)");

            var reader = new StringBuilder();
            if (!writeEntity)
                reader.AppendLine("            EntityDependency.AddReader(request);");

            reader.AppendLine("            WorldDependency.AddReader(request);");

            foreach (var arg in source.Arguments)
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
            using (SwapDependency.BeginContext())
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
        
        void ImplementForeach()
        {
            sb.Append($@"
        
        public Enumerator GetEnumerator() => new(this);
");

            var iterationInit = new StringBuilder();
            var accessorFields = new StringBuilder();
            var accessorInit = new StringBuilder();
            foreach (var arg in source.Arguments)
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
                    
                    iterationInit.Append($@"
                    iter.Accessor{arg.Symbol.Name} = Accessor{arg.Symbol.Name};
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
                        $"iter.Accessor{arg.Symbol.Name} = Accessor{arg.Symbol.Name}; ");
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
            var iterationFields = new StringBuilder("            public UEntityHandle Handle;\n");
            foreach (var arg in source.Arguments)
            {
                if (arg.IsOption || arg.Modifier == QueryArgument.EModifier.None)
                    continue;

                var accessorTypeName = $"EntityComponentAccessor<{arg.Symbol.GetTypeName()}>";
                var typeName = arg.Symbol.GetTypeName();
                if (arg.Custom is {ViaAccessor: { } viaAccessor})
                {
                    accessorTypeName = viaAccessor.FieldType ?? accessorTypeName;
                    typeName = viaAccessor.ValueType ?? typeName;
                }

                var perm = arg.Custom.DisableReferenceWrapper
                    ? string.Empty
                    : arg.CanWrite
                        ? "ref"
                        : "ref readonly";
                var accessPerm = arg.Custom.DisableReferenceWrapper
                    ? string.Empty
                    : "ref";

                var access = arg.Custom is {ViaAccessor: { } accessor}
                    ? $"{accessor.GetAccess($"Accessor{arg.Symbol.Name}", "Handle", accessPerm)};"
                    : $"{accessPerm} Accessor{arg.Symbol.Name}[handle];";

                iterationFields.AppendLine($"            [EditorBrowsable(EditorBrowsableState.Never)] public {accessorTypeName} Accessor{arg.Symbol.Name};\n" +
                                           $"            public {perm} {typeName} {arg.PublicName} => {access}\n");
            }

            iteration.Append($@"{iterationFields}
        }}");
            sb.AppendLine(iteration.ToString());
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

        public Iteration First()
        {{
            if (!Any()) {{
                return default;
            }}

            var enumerator = GetEnumerator();
            enumerator.MoveNext();
            return enumerator.Current;
        }}
");
        }

        BeginNamespace();
        {
            BeginParentTypes();
            {
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
                        ImplementForeach();
                    }

                    Dependency();
                    AdditionalFunctions();
                }
                EndStruct();
            }
            EndParentTypes();
        }
        EndNamespace();
        
        var fileName = $"{(source.Parent == null ? "" : $"{source.Parent.Name}.")}{source.Name}";
        Context.AddSource($"QUERY.{Path.GetFileNameWithoutExtension(source.FilePath)}.{fileName}", sb.ToString());
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
                    if (queryInterface.TypeArguments.Length == 0)
                    {
                        Context.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor(
                                id: "REVQUERY001",
                                title: "No T parameter found on Query",
                                messageFormat: $"Parameter T was not found on query, introduce it as tuple.",
                                category: "revecs.Query",
                                defaultSeverity: DiagnosticSeverity.Error,
                                isEnabledByDefault: true),
                            queryInterface.Locations.FirstOrDefault()
                        ));
                        
                        continue;   
                    }
                    
                    var arg = queryInterface.TypeArguments[0];
                    Log(1, "Param: " + arg);

                    var header = Array.Empty<QueryArgument>();
                    if (arg.IsTupleType)
                    {
                        var tupleElements = ((INamedTypeSymbol) arg).TupleElements;

                        header = tupleElements.Select(t =>
                        {
                            var modifier = (INamedTypeSymbol) t.Type;
                            var component = (INamedTypeSymbol) modifier.TypeArguments[0];
                            var name = component.Name;
                            if (t.Name != t.CorrespondingTupleField?.Name)
                                name = t.Name;

                            Log(3, $"Found Argument {t.Name} ({name}) compo: {component}");

                            var eModifier = modifier.Name.ToLower() switch
                            {
                                "write" or "read" or "all" => QueryArgument.EModifier.All,
                                "or" => QueryArgument.EModifier.Or,
                                "none" => QueryArgument.EModifier.None,
                                _ => throw new InvalidOperationException("Invalid")
                            };

                            Log(4, "Option? " + (modifier.Name is not ("Write" or "Read")));

                            return new QueryArgument
                            {
                                Symbol = component,
                                PublicName = name,
                                CanWrite = modifier.Name is "Write",
                                IsOption = modifier.Name is not ("Write" or "Read"),
                                Modifier = eModifier,
                                Custom = ComponentGenerator.GetCustomAccess(Compilation, component)
                            };
                        }).ToArray();
                    }
                    else
                    {
                        var modifier = (INamedTypeSymbol) arg;
                        var component = (INamedTypeSymbol) modifier.TypeArguments[0];
                        var name = component.Name;

                        Log(3, $"Found Argument ({name}) compo: {component}");

                        var eModifier = modifier.Name.ToLower() switch
                        {
                            "write" or "read" or "all" => QueryArgument.EModifier.All,
                            "or" => QueryArgument.EModifier.Or,
                            "none" => QueryArgument.EModifier.None,
                            _ => throw new InvalidOperationException("Invalid")
                        };

                        Log(4, "Option? " + (modifier.Name is not ("Write" or "Read")));

                        (header = new QueryArgument[1])[0] = new QueryArgument
                        {
                            Symbol = component,
                            PublicName = name,
                            CanWrite = modifier.Name is "Write",
                            IsOption = modifier.Name is not ("Write" or "Read"),
                            Modifier = eModifier,
                            Custom = ComponentGenerator.GetCustomAccess(Compilation, component)
                        };
                    }
                    
                    var source = new QuerySource
                    (
                        symbol.ContainingType,
                        symbol.ContainingNamespace?.ToString(),
                        symbol.Name,
                        tree.FilePath,
                        header,
                        IsRecord: symbol.IsRecord
                    );
                    
                    GenerateQuery(source);
                }
            }
        }
    }
}