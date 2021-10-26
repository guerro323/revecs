using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharpSyntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree;

namespace revecs.Generator;

public class SystemGenerator
{
    public readonly GeneratorExecutionContext Context;
    public readonly SyntaxReceiver Receiver;

    public readonly QueryGenerator QueryGenerator;
    public readonly CommandGenerator CommandGenerator;
    public readonly ComponentGenerator ComponentGenerator;

    public SystemGenerator(
        QueryGenerator queryGenerator,
        CommandGenerator commandGenerator,
        ComponentGenerator componentGenerator,
        GeneratorExecutionContext context, SyntaxReceiver syntaxReceiver,
        ref Compilation compilation
    )
    {
        QueryGenerator = queryGenerator;
        CommandGenerator = commandGenerator;
        ComponentGenerator = componentGenerator;

        Context = context;
        Receiver = syntaxReceiver;

        Compilation = compilation;
        
        GenerateHelpers();
        CreateSystems();

        compilation = Compilation;
    }

    private void Log<T>(int indent, T txt)
    {
        Receiver.Log.Add(string.Join(null, Enumerable.Repeat("\t", indent)) + txt);
    }

    public Compilation Compilation;

    private void GenerateHelpers()
    {
        const string helpersSource =
            @"
using System;
using System.Runtime.CompilerServices;
namespace revecs
{
    internal class RevolutionSystemAttribute : Attribute {}

    /// <summary>
    /// Depend on another <see cref=""ISystem""/>
    /// </summary>
    /// <list type=""table"">
    /// <item>
    ///     <term><see cref=""Type""/></term>
    ///     <description>If the system struct was auto-generated (not defined by the client) then it must be fully qualified.</description>
    /// </item>
    /// <item>
    ///     <term><see cref=""RequireCompletion""/></term>
    ///     <description>If true and the dependency was canceled or not queued, the system will not be run.</description>
    /// </item>
    /// </list>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    internal class DependOnAttribute : Attribute 
    {
        public readonly Type Type;
        public readonly bool RequireCompletion;

        private DependOnAttribute()
        {
            throw new InvalidOperationException(""not initialized"");
        }
        
        /// <param name=""type"">The system type on which to depend.</param>
        public DependOnAttribute(Type type) {}
        public DependOnAttribute(string type) {}
        
        /// <param name=""type"">Whether or not the system can only execute if the dependency was successfully completed</param>
        /// <param name=""requireCompletion"">The system type on which to depend.</param>
        public DependOnAttribute(Type type, bool requireCompletion = true) {}
        public DependOnAttribute(string type, bool requireCompletion = true) {}
    }

    /// <summary>
    /// Make another <see cref=""ISystem""/> depend on the created one.
    /// </summary>
    /// <list type=""table"">
    /// <item>
    ///     <term><see cref=""Type""/></term>
    ///     <description>If the system struct was auto-generated (not defined by the client) then it must be fully qualified.</description>
    /// </item>
    /// <item>
    ///     <term><see cref=""RequireCompletion""/></term>
    ///     <description>If true and the dependency was canceled or not queued, the system will not be run.</description>
    /// </item>
    /// </list>
    [AttributeUsage(AttributeTargets.Method)]
    internal class AddForeignDependencyAttribute : Attribute 
    {
        public readonly Type Type;
        public readonly bool RequireCompletion;

        private AddForeignDependencyAttribute()
        {
            throw new InvalidOperationException(""not initialized"");
        }
        
        /// <param name=""type"">The system type on which to depend.</param>
        public AddForeignDependencyAttribute(Type type) {}
        public AddForeignDependencyAttribute(string type) {}
        
        /// <param name=""type"">Whether or not the system can only execute if the dependency was successfully completed</param>
        /// <param name=""requireCompletion"">The system type on which to depend.</param>
        public AddForeignDependencyAttribute(Type type, bool requireCompletion = true) {}
        public AddForeignDependencyAttribute(string type, bool requireCompletion = true) {}
    }

    internal class ParamAttribute : Attribute {}
    internal class OptionalAttribute : Attribute {}
}
";
        Context.AddSource("SystemAttributes", helpersSource);
        Compilation = Compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText
            (
                helpersSource,
                (Context.Compilation as CSharpCompilation)?.SyntaxTrees[0].Options as CSharpParseOptions
            )
        );
    }

    private void CreateSystems()
    {
        foreach (var tree in Compilation.SyntaxTrees)
        {
            var semanticModel = Compilation.GetSemanticModel(tree);

            foreach (var declaredMethod in tree
                .GetRoot()
                .DescendantNodesAndSelf()
                .OfType<MethodDeclarationSyntax>())
            {
                if (tree.FilePath.Contains("Generated/Generator"))
                    continue;

                var symbol = (IMethodSymbol) ModelExtensions.GetDeclaredSymbol(semanticModel, declaredMethod)!;

                // is system
                var systemAttribute = symbol
                    .GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass.Name == "RevolutionSystemAttribute");

                if (systemAttribute != null)
                {
                    Log(0, "Found System: " + symbol + ", File: " + tree.FilePath);
                    
                    FoundSystem(tree, declaredMethod, symbol, systemAttribute);
                }
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetParentTypes(IMethodSymbol method)
    {
        var type = method.ContainingType;
        while (type != null)
        {
            yield return type;
            type = type.ContainingType;
        }
    }

    record ParamSource(string Name, INamedTypeSymbol Type);
    record SystemDependencySource(string TypeName, bool RequireSuccess);
    record ForeignSystemDependencySource(string TypeName, bool RequireSuccess);

    private void FoundSystem(SyntaxTree tree, MethodDeclarationSyntax declare, IMethodSymbol symbol, AttributeData _)
    {
        var model = Compilation.GetSemanticModel(tree);
        
        var sb = new StringBuilder();

        void Usings()
        {
            sb.Append(@"using revecs.Core;
using revecs.Utility;
using revecs.Query;
using revecs.Systems;
using revtask.Core;
using revtask.Helpers;
using revecs.Extensions.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
");
            
            var imports = declare.SyntaxTree.GetRoot().DescendantNodes().Where(node => node is UsingDirectiveSyntax);
            foreach (var import in imports)
            {
                sb.AppendLine(import.ToString());
            }
        }

        void BeginNamespace()
        {
            if (symbol.ContainingNamespace is { } ns)
                sb.AppendFormat("namespace {0}\n{{\n", ns.ToString());
        }

        void EndNamespace()
        {
            if (symbol.ContainingNamespace is { })
                sb.AppendFormat("}}\n");
        }
        
        var isParentAPartialSystem = symbol.ContainingType is { } parent
                                     && parent
                                         .AllInterfaces
                                         .Any(i => i.Name == "ISystem");

        var parentTypes = GetParentTypes(symbol)
            .Skip(isParentAPartialSystem ? 1 : 0)
            .Reverse()
            .ToList();
        
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
            foreach (var parent in parentTypes) sb.AppendLine("}");
        }

        var arguments = new Dictionary<string, object>();
        void CreateArguments()
        {
            foreach (var arg in symbol.Parameters)
            {
                Log(1, arg.Name);
                Log(2, arg.Type + " : " + arg.Type.GetType());

                //
                // Params
                //
                if (arg.GetAttributes().Any(a => a.AttributeClass!.Name == "ParamAttribute"))
                {
                    if (arg.Type is INamedTypeSymbol named)
                    {
                        arguments[arg.Name] = new ParamSource(arg.Name, named);
                    }
                    else
                    {
                        Context.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor(
                                id: "REVSYS001",
                                title: "Invalid type on parameter with ParamAttribute",
                                messageFormat: $"Parameter '{arg.Name}' with ParamAttribute should be a concrete type.",
                                category: "revecs.System",
                                defaultSeverity: DiagnosticSeverity.Error,
                                isEnabledByDefault: true),
                            arg.Locations.FirstOrDefault()
                        ));
                    }
                }
                //
                // Queries
                //
                else if (arg.GetAttributes().Any(a => a.AttributeClass!.Name
                             is "Query" or "QueryAttribute"
                             or "Singleton" or "SingletonAttribute"))
                {
                    var options = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                    var queryName = default(string);
                    var validQueryName = default(string);
                    
                    var isAnonymousQuery = arg.Type.Locations.IsEmpty;
                    if (isAnonymousQuery)
                    {
                        queryName = arg.Type.Name + "<";
                        queryName += string.Join(',',
                            ((INamedTypeSymbol) arg.Type).TypeArguments.Select((t, i) => $"T{i}"));
                        queryName += ">";

                        validQueryName = arg.Type.Name + "<";
                        validQueryName += string.Join(',',
                            ((INamedTypeSymbol) arg.Type).TypeArguments.Select(_ => "global::System.ValueTuple"));
                        validQueryName += ">";

                        foreach (var t in ((INamedTypeSymbol) arg.Type).TypeArguments)
                            options.Add((INamedTypeSymbol) t);
                        
                        if (arg.GetAttributes().Any(a => a.AttributeClass!.Name is "Singleton" or "SingletonAttribute"))
                        {
                            options.Add(QueryGenerator.GetSingletonTypeSymbol());
                        }
                    }
                    else
                    {
                        foreach (var t in arg.Type.AllInterfaces)
                            options.Add(t);
                    }

                    if (arg.GetAttributes().Any(a => a.AttributeClass!.Name is "Optional" or "OptionalAttribute"))
                    {
                        var optionalAttribute = Compilation.GetUnknownType("global::revecs.OptionalAttribute");
                        if (optionalAttribute == null)
                            throw new NullReferenceException(nameof(optionalAttribute));
                        options.Add(optionalAttribute);
                    }
                        
                    var source = new QuerySource(
                        symbol.ContainingType,
                        symbol.ContainingNamespace?.ToString(),
                        arg.Type.Name,
                        tree.FilePath,
                        options.ToArray(),
                        StructureName: queryName,
                        ValidStructureName: validQueryName,
                        AlreadyGenerated: !isAnonymousQuery
                    );
                    arguments.Add(arg.Name, source);
                }
                //
                // Commands
                //
                else if (arg.GetAttributes().Any(a => a.AttributeClass!.Name
                             is "Cmd" or "CmdAttribute"))
                {
                    var options = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                    var cmdName = default(string);
                    var validCmdName = default(string);
                    
                    var isAnonymousCmd = arg.Type.Locations.IsEmpty;
                    if (isAnonymousCmd)
                    {
                        cmdName = arg.Type.Name + "<";
                        cmdName += string.Join(',',
                            ((INamedTypeSymbol) arg.Type).TypeArguments.Select((t, i) => $"T{i}"));
                        cmdName += ">";

                        validCmdName = arg.Type.Name + "<";
                        validCmdName += string.Join(',',
                            ((INamedTypeSymbol) arg.Type).TypeArguments.Select(_ => "global::System.ValueTuple"));
                        validCmdName += ">";

                        foreach (var t in ((INamedTypeSymbol) arg.Type).TypeArguments)
                            options.Add((INamedTypeSymbol) t);
                    }
                    else
                    {
                        foreach (var t in arg.Type.AllInterfaces)
                            options.Add(t);
                    }

                    var source = new CommandSource(
                        symbol.ContainingType,
                        symbol.ContainingNamespace?.ToString(),
                        arg.Type.Name,
                        tree.FilePath,
                        options.ToArray(),
                        StructureName: cmdName,
                        ValidStructureName: validCmdName,
                        AlreadyGenerated: !isAnonymousCmd
                    );
                    arguments.Add(arg.Name, source);
                }
            }

            var attrs = symbol.GetAttributes();
            for (var index = 0; index < attrs.Length; index++)
            {
                var attr = attrs[index];
                Log(1, "Attribute [" + attr.AttributeClass.Name + "]");
                if (attr.AttributeClass!.Name is "DependOnAttribute" or "AddForeignDependencyAttribute")
                {
                    Log(2, "Constructors Length: " + attr.ConstructorArguments.Length);

                    TypedConstant type, require = default;
                    type = attr.ConstructorArguments[0];
                    if (attr.ConstructorArguments.Length > 1)
                        require = attr.ConstructorArguments[1];

                    var fullFormat = string.Empty;
                    var shortName = string.Empty;
                    
                    var isGenerated = false;
                    if (type.Value is string str)
                    {
                        var method = declare.GetMethodSymbolFromNameOf(model, index);

                        fullFormat = $"{method.ContainingType.GetTypeName()}.__{method.Name}System";
                        shortName = method.Name;
                    }
                    else
                    {
                        fullFormat = ((INamedTypeSymbol) type.Value!).GetTypeName();
                        shortName = ((INamedTypeSymbol) type.Value!).Name;
                        
                        isGenerated = ((INamedTypeSymbol) type.Value!).Locations.IsEmpty;
                    }

                    var required = require.Value as bool? ?? false;

                    Log(2, $"{fullFormat} ({required}) Of Generated? {isGenerated}");

                    if (isGenerated && !fullFormat.StartsWith("global::"))
                    {
                        Context.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor(
                                id: "REVSYS002",
                                title: "",
                                messageFormat:
                                $"System '{symbol.Name}' [{attr.AttributeClass.Name}] should be fed with a constructed type. (Encapsulate other system function into an user-created ISystem structure, or fully qualify the type with global::)",
                                category: "revecs.System",
                                defaultSeverity: DiagnosticSeverity.Error,
                                isEnabledByDefault: true),
                            symbol.Locations.FirstOrDefault()
                        ));
                    }
                    else
                    {
                        if (attr.AttributeClass.Name is "AddForeignDependencyAttribute")
                        {
                            arguments[$"{shortName}"] =
                                new ForeignSystemDependencySource(fullFormat, required);
                        }
                        else
                        {
                            arguments[$"{shortName}"] =
                                new SystemDependencySource(fullFormat, required);
                        }
                    }
                }
            }

            foreach (var (_, obj) in arguments)
            {
                if (obj is QuerySource {AlreadyGenerated: false} querySource)
                    QueryGenerator.GenerateQuery(querySource);
                if (obj is CommandSource {AlreadyGenerated: false} commandSource)
                    CommandGenerator.GenerateCommand(commandSource);
            }
        }

        var systemName = $"__{symbol.Name}System";
        if (isParentAPartialSystem)
            systemName = symbol.ContainingType!.Name;

        var needConstructor = false;
        
        void BeginSystem()
        {
            if (isParentAPartialSystem)
            {
                sb.AppendLine($"    public partial struct {systemName}\n    {{");
            }
            else
            {
                sb.AppendLine($"    public partial struct {systemName} : ISystem\n    {{");
            }

            needConstructor = arguments.Any(pair => pair.Value is ParamSource);
            if (needConstructor)
            {
                var init = string.Join(
                    ',',
                    arguments
                        .Select(pair =>
                        {
                            var (name, obj) = pair;
                            if (obj is not ParamSource paramSource)
                                return default;

                            return (name, obj: paramSource);
                        })
                        .Where(pair => pair.obj != null!)
                        .Select(pair =>
                        {
                            var (name, obj) = pair;
                            return $"{obj.Type.GetTypeName()} {name}";
                        })
                );
                
                var body = string.Join(
                    "\n            ",
                    arguments
                        .Select(pair =>
                        {
                            var (name, obj) = pair;
                            if (obj is not ParamSource paramSource)
                                return default;

                            return (name, obj: paramSource);
                        })
                        .Where(pair => pair.obj != null!)
                        .Select(pair =>
                        {
                            var (name, obj) = pair;
                            return $"this.{name} = {name};";
                        })
                );

                sb.AppendLine($@"
        public {systemName}({init})
        {{
            this = default;
            {body}
        }}
");
            }
        }

        void EndSystem()
        {
            sb.AppendLine("    }");
        }

        void SystemVariables()
        {
            sb.AppendLine("        private SwapDependency _entityDependency;");
            sb.AppendLine("        private ComponentType<BufferData<SystemDependencies>> _systemDependenciesType;");
            sb.AppendLine("        private ComponentType<JobRequest> _currentSystemJobType;");
            sb.AppendLine("        private ComponentType<SystemState> _systemStateType;");
            
            foreach (var (name, obj) in arguments)
            {
                if (obj is QuerySource querySource)
                    sb.AppendLine($"        private {querySource.GetDisplayName()} {name};");

                if (obj is CommandSource commandSource)
                    sb.AppendLine($"        private {commandSource.GetDisplayName()} {name};");

                if (obj is ParamSource paramSource)
                    sb.AppendLine($"        private {paramSource.Type.GetTypeName()} {name};");

                if (obj is SystemDependencySource or ForeignSystemDependencySource)
                    sb.AppendLine($"        private ComponentType {name}Type;");
            }
        }

        void CreateFunction()
        {
            var init = new StringBuilder();
            foreach (var (name, obj) in arguments)
            {
                if (obj is QuerySource query)
                {
                    init.Append($"            {name} = new {query.GetDisplayName()}(world);");
                }
                
                if (obj is CommandSource cmd)
                {
                    init.Append($"            {name} = new {cmd.GetDisplayName()}(world);");
                }

                if (obj is SystemDependencySource dep)
                {
                    init.Append($"            {name}Type = world.GetSystemType<{dep.TypeName}>();");
                }
                
                if (obj is ForeignSystemDependencySource foreign)
                {
                    init.Append($"            {name}Type = world.GetSystemType<{foreign.TypeName}>();");
                }

                init.AppendLine();
            }
            
            sb.AppendLine(@$"
        public bool Create(SystemHandle systemHandle, RevolutionWorld world) 
        {{
            world.SetSystem<{systemName}>(systemHandle);

            // This is done so that the initial method doesn't appear as unused
            _ = nameof({symbol.Name});
            _entityDependency = world.GetEntityDependency();
            _systemDependenciesType = SystemDependencies.GetComponentType(world);
            _currentSystemJobType = CurrentSystemJobRequest.GetComponentType(world);
            _systemStateType = SystemStateStatic.GetComponentType(world);
{init}
            return true;
        }}
");
        }
        
        void PreQueueFunction()
        {
            var fromSource = new StringBuilder();

            foreach (var (name, obj) in arguments)
            {
                if (obj is SystemDependencySource source)
                {
                    var addSection =
                        $"new() {{ Other = world.GetSystemHandle({name}Type), RequireSuccess = {source.RequireSuccess.ToString().ToLower()} }}";
                    
                    fromSource.AppendLine(
                        $@"            world.GetComponentData(systemHandle, _systemDependenciesType)
                    .Add({addSection});"
                    );
                }
                else if (obj is ForeignSystemDependencySource foreignSource)
                {
                    var addSection =
                        $"new() {{ Other = systemHandle, RequireSuccess = {foreignSource.RequireSuccess.ToString().ToLower()} }}";

                    fromSource.AppendLine(
                        $@"            world.GetComponentData(world.GetSystemHandle({name}Type), _systemDependenciesType)
                    .Add({addSection});"
                    );
                }
            }

            sb.AppendLine(@$"
        public void PreQueue(SystemHandle systemHandle, RevolutionWorld world) 
        {{
{fromSource}
        }}
");     
        }

        void QueueFunction()
        {
            var fromSource = new StringBuilder();
            foreach (var (name, obj) in arguments)
            {
                if (obj is QuerySource or CommandSource or ParamSource)
                {
                    fromSource.Append("            ");
                    fromSource.AppendLine($"job.{name} = {name};");
                }
            }
            
            sb.AppendLine(@$"
        public JobRequest Queue(SystemHandle systemHandle, RevolutionWorld world, IJobRunner runner) 
        {{
            Job job;
            job.__world = world;
            job.systemHandle = systemHandle; 
            job._entityDependency = _entityDependency;
            job._currentSystemJobType = _currentSystemJobType;
            job._systemDependenciesType = _systemDependenciesType;
            job._systemStateType = _systemStateType;
{fromSource}
            return runner.Queue(job);
        }}
");     
        }

        void JobStruct()
        {
            var fieldFromSource = new StringBuilder();
            foreach (var (name, obj) in arguments)
            {
                if (obj is QuerySource querySource)
                {
                    fieldFromSource.Append("            ");
                    fieldFromSource.AppendLine($"public {querySource.GetDisplayName()} {name};");
                }
                
                if (obj is CommandSource commandSource)
                {
                    fieldFromSource.Append("            ");
                    fieldFromSource.AppendLine($"public {commandSource.GetDisplayName()} {name};");
                }
                
                if (obj is ParamSource paramSource)
                {
                    fieldFromSource.Append("            ");
                    fieldFromSource.AppendLine($"public {paramSource.Type.GetTypeName()} {name};");
                }
            }

            var interfaceSet = new HashSet<string>()
            {
                "IJob",
                "IJobExecuteOnCondition"
            };
            var additional = new StringBuilder();
            var setHandle = new StringBuilder();

            var canExecute = new StringBuilder();

            // Todo: access RequireReader in Commands
            var requireReader = true;

            if (requireReader)
            {
                interfaceSet.Add("IJobSetHandle");
            }
            
            // Todo: detect from additional Query arguments (after IQuery) (eg: IQueryRemoveEntities)
            var writeEntity = false;
            if (writeEntity)
            {
                canExecute.AppendLine("_entityDependency.TrySwap(runner, info.Request)");
            }
            else
            {
                setHandle.AppendLine("                _entityDependency.AddReader(handle);");
                canExecute.AppendLine("_entityDependency.IsCompleted(runner, info.Request)");
            }

            foreach (var (name, obj) in arguments)
            {
                if (obj is QuerySource or CommandSource)
                {
                    canExecute.Append("                   ");
                    canExecute.AppendLine($"&& {name}.DependencySwap(runner, info.Request)");

                    setHandle.Append("                ");
                    setHandle.AppendLine($"{name}.AddDependencyReader(runner, handle);");
                }
            }

            var ret = "__world.GetComponentData(systemHandle, _systemStateType) = SystemState.RanToCancellation;";
            
            var earlyReturn = new StringBuilder();
            foreach (var (name, obj) in arguments)
            {
                if (obj is QuerySource querySource)
                {
                    if (querySource.Header.Any(t => t.Name is QueryGenerator.OptionalType or $"{QueryGenerator.OptionalType}Attribute"))
                        continue;
                    
                    earlyReturn.Append("                ");
                    earlyReturn.AppendLine($"if ({name}.Any() == false) {{ {ret} return; }}");
                }
            }
            
            additional.Append($@"
            public void SetHandle(IJobRunner runner, JobRequest handle)
            {{
{setHandle}
            }}
");

            sb.AppendLine(@$"
        private struct Job : IJob, IJobExecuteOnCondition 
        {{
            public RevolutionWorld __world;

            public SwapDependency _entityDependency;
            public SystemHandle systemHandle;

            public ComponentType<JobRequest> _currentSystemJobType;
            public ComponentType<BufferData<SystemDependencies>> _systemDependenciesType;
            public ComponentType<SystemState> _systemStateType;

{fieldFromSource}
            
            public int SetupJob(JobSetupInfo info) => 1;
    
            public void Execute(IJobRunner runner, JobExecuteInfo info)
            {{
                // Short-circuit, a dependency of ours was needed to be completed but wasn't.
                if (__world.GetComponentData(systemHandle, _systemStateType) == SystemState.RanToCancellation)
                    return;

{earlyReturn}
                Do();

                __world.GetComponentData(systemHandle, _systemStateType) = SystemState.RanToCompletion;
            }}

            public bool CanExecute(IJobRunner runner, JobExecuteInfo info)
            {{
                var dependencies = __world.GetComponentData(systemHandle, _systemDependenciesType);
                foreach (var dep in dependencies)
                {{
                    var request = __world.GetComponentData(dep.Other, _currentSystemJobType);
                    var state = __world.GetComponentData(dep.Other, _systemStateType);
                    if (dep.RequireSuccess)
                    {{
                        if ((state == SystemState.Queued && request.Equals(default))
                            || state == SystemState.RanToCancellation)
                        {{
                            // Force cancel and return true.
                            // Dependencies of this system that require completion will do the same.
                            __world.GetComponentData(systemHandle, _systemStateType) = SystemState.RanToCancellation;
                            return true;
                        }}
                    }}

                    // Less checks for when it's doesn't require full completion
                    // 1. If the dep wasn't yet queued, early return
                    // 2. If the dep isn't yet completed, early return
                    if (state < SystemState.Queued || !runner.IsCompleted(request))
                        return false;
                }}

                return {canExecute};
            }}

            private void Do()
            {{
{declare.Body}
            }}

{additional}
        }}
");
        }

        void CreateShadowFunction()
        {
            if (isParentAPartialSystem)
                return;

            if (!needConstructor)
            {
                sb.Append($@"
    public static void {symbol.Name}(SystemGroup systemGroup)
    {{
        systemGroup.Add(new {systemName}());
    }}
");

                return;
            }

            var filteredArgs = arguments
                .Select(pair =>
                {
                    var (name, obj) = pair;
                    if (obj is not ParamSource paramSource)
                        return default;

                    return (name, obj: paramSource);
                })
                .Where(pair => pair.obj != null!)
                .ToArray();

            var field = string.Join(
                "\n            ",
                filteredArgs.Select(pair =>
                    {
                        var (name, obj) = pair;
                        return $"public {obj.Type.GetTypeName()} {name};";
                    })
            );

            var param = string.Join(
                ',',
                filteredArgs.Select(pair =>
                {
                    var (name, obj) = pair;
                    return $"{obj.Type.GetTypeName()} {name}";
                })
            );

            var set = string.Join(
                ',',
                filteredArgs.Select(pair =>
                    {
                        var (name, obj) = pair;
                        if (obj is not ParamSource paramSource)
                            return default;

                        return (name, obj: paramSource);
                    })
                    .Where(pair => pair.obj != null!)
                    .Select(pair =>
                    {
                        var (name, obj) = pair;
                        return $"opt.{name}";
                    })
            );

            var option = new StringBuilder();
            if (filteredArgs.Length == 1)
            {
                option.Append(@$"
            public static implicit operator Option({filteredArgs[0].obj.Type.GetTypeName()} arg) => new() {{ {filteredArgs[0].name} = arg }};
");
            }
            else
            {
                var tupleSet = string.Join(
                    ',',
                    filteredArgs.Select(a =>
                    {
                        return $"{a.name} = arg.{a.name}";
                    })
                );
                
                option.Append($@"
            public static implicit operator Option(({param}) arg) => new() {{ {tupleSet} }};
");
            }

            sb.Append($@"
    public static Action<SystemGroup> {symbol.Name}({systemName}.Option opt)
    {{
        return g => g.Add(new {systemName}({set}));
    }}

    public partial struct {systemName}
    {{
        public struct Option
        {{
            {field}

            {option}
        }}
    }}
");
        }

        Usings();
        BeginNamespace();
        {
            BeginParentTypes();
            {
                CreateArguments();
                
                BeginSystem();
                {
                    SystemVariables();
                    CreateFunction();
                    PreQueueFunction();
                    QueueFunction();
                    JobStruct();
                }
                EndSystem();

                CreateShadowFunction();
            }
            EndParentTypes();
        }
        EndNamespace();

        var fileName = $"{(symbol.ContainingType == null ? "" : $"{symbol.ContainingType.Name}.")}{symbol.Name}";
        Context.AddSource($"{Path.GetFileNameWithoutExtension(tree.FilePath)}.{fileName}", "#pragma warning disable\n" + sb.ToString());
    }
}