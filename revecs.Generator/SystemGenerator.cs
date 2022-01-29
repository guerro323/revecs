using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharpSyntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree;

namespace revecs.Generator;

public class SystemQuery
{
    public bool IsResource;
    public bool IsOptional;
    public QuerySource Source;
}

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
        CreateSystems();

        compilation = Compilation;
    }

    private void Log<T>(int indent, T txt)
    {
        Receiver.Log.Add(string.Join(null, Enumerable.Repeat("\t", indent)) + txt);
    }

    public Compilation Compilation;

    private void CreateSystems()
    {
        foreach (var tree in Compilation.SyntaxTrees)
        {
            var semanticModel = Compilation.GetSemanticModel(tree);

            foreach (var declare in tree
                .GetRoot()
                .DescendantNodesAndSelf()
                .OfType<TypeDeclarationSyntax>())
            {
                if (declare is InterfaceDeclarationSyntax)
                    continue;
                
                if (tree.FilePath == string.Empty)
                    continue;
                
                if (tree.FilePath.Contains("Generated/Generator"))
                    continue;

                var symbol = (ITypeSymbol) ModelExtensions.GetDeclaredSymbol(semanticModel, declare)!;

                // is system
                var systemInterface = symbol
                    .Interfaces
                    .FirstOrDefault(i => i.Name == "IRevolutionSystem");

                if (systemInterface != null)
                {
                    Log(0, "Found System: " + symbol + ", File: " + tree.FilePath);
                    
                    FoundSystem(tree, declare, symbol);
                }
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetParentTypes(ISymbol method)
    {
        var type = method.ContainingType;
        while (type != null)
        {
            yield return type;
            type = type.ContainingType;
        }
    }

    private void FoundSystem(SyntaxTree tree, TypeDeclarationSyntax declare, ITypeSymbol symbol)
    {
        var model = Compilation.GetSemanticModel(tree);
        
        var sb = new StringBuilder();

        void Usings()
        {
            sb.Append(@"using revecs.Core;
using revecs.Utility;
using revecs.Querying;
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
                sb.Append($"namespace {ns.ToString()};\n\n");
        }
        
        var parentTypes = GetParentTypes(symbol)
            .Reverse()
            .ToList();
        
        if (symbol.GetMembers("Body").FirstOrDefault() is not IMethodSymbol bodyMethod)
            throw new InvalidOperationException("Body not found");

        var bodySyntax = bodyMethod.DeclaringSyntaxReferences
            .First()
            .GetSyntax() as MethodDeclarationSyntax;
        
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
            void GetNodes<T>(SyntaxNode up, List<T> list, int indent = 0)
            {
                foreach (var node in up.ChildNodes())
                {
                    //Log(1, string.Join("", Enumerable.Repeat("  ", indent)) + node.GetType().Name);
                    if (node is T s)
                    {
                        list.Add(s);
                        GetNodes(node, list, indent + 1);
                    }
                    else
                        GetNodes(node, list, indent + 1);
                }
            }
            
            var list = new List<InvocationExpressionSyntax>();
            GetNodes(bodySyntax, list);

            var replace = new Dictionary<SyntaxNode, SyntaxNode>();

            void Replace(SyntaxNode old, SyntaxNode n)
            {
                replace[old] = n;
            }

            var i = 0;
            foreach (var node in list)
            {
                i += 1;

                var identifier = node.ChildNodes().First().GetFirstToken().Text;
                var isRes = identifier is "RequiredResource" or "OptionalResource";
                var isQuery = identifier is "RequiredQuery" or "OptionalQuery";

                if (isQuery)
                {
                    Log(1, $"Found {identifier}");
                    var queryArgs = node.ChildNodes().OfType<ArgumentListSyntax>().First();

                    var args = new List<QueryArgument>();
                    foreach (var arg in queryArgs.ChildNodes())
                    {
                        var modifierList = new List<GenericNameSyntax>();
                        var typeList = new List<IdentifierNameSyntax>();
                        var nameList = new List<LiteralExpressionSyntax>();
                        GetNodes(arg, modifierList);
                        GetNodes(arg, typeList);
                        GetNodes(arg, nameList);

                        if (typeList.Count == 0)
                        {
                            throw new InvalidOperationException("Type list shouldn't be empty");
                        }

                        if (modifierList.Count == 0)
                        {
                            throw new InvalidOperationException("Modifier list shouldn't be empty");
                        }

                        var t = typeList[0];
                        var modifier = modifierList[0].GetFirstToken().Text;
                        var typeName = t.GetFirstToken().Text;
                        var alternativeName = nameList.Count == 0
                            ? typeName
                            : nameList[0].GetFirstToken().Value;

                        Log(1, "searchin for  " + typeName);

                        var typeSymbolInfo = Compilation.GetSemanticModel(tree, true).GetSymbolInfo(t);
                        var typeSymbol =
                            (typeSymbolInfo.Symbol ?? typeSymbolInfo.CandidateSymbols.FirstOrDefault()) as
                            INamedTypeSymbol;
                        if (typeSymbol == null)
                            throw new InvalidOperationException($"Type '{typeName}' not found");

                        Log(2, $"{modifier.ToLower()} {typeName}:{alternativeName} {typeSymbol.GetTypeName()}");

                        // (modifier, typeSymbol, alternativeName.ToString())

                        QueryArgument.EModifier eModifier = modifier.ToLower() switch
                        {
                            "write" or "read" or "all" => QueryArgument.EModifier.All,
                            "or" => QueryArgument.EModifier.Or,
                            "none" => QueryArgument.EModifier.None,
                            _ => throw new InvalidOperationException("unknown identifier")
                        };
                        args.Add(new QueryArgument
                        {
                            Symbol = typeSymbol,
                            PublicName = alternativeName.ToString(),
                            CanWrite = modifier == "Write",
                            IsOption = modifier != "Write" && modifier != "Read",
                            Modifier = eModifier,
                            Custom = ComponentGenerator.GetCustomAccess(Compilation, typeSymbol)
                        });
                    }

                    var source = new QuerySource(
                        (INamedTypeSymbol) symbol,
                        symbol.ContainingNamespace?.ToString(),
                        $"query{i}",
                        tree.FilePath,
                        args.ToArray(),
                        StructureName: $"__Query{i}",
                        ValidStructureName: $"__Query{i}",
                        AlreadyGenerated: false
                    );
                    arguments.Add($"query{i}", new SystemQuery
                    {
                        IsOptional = identifier.StartsWith("Optional"),
                        IsResource = false,
                        Source = source
                    });
                    Replace(node, SyntaxFactory.IdentifierName($"query{i}"));
                    
                }
                else if (isRes)
                {
                    Log(1, $"Found {identifier}");
                    var queryArgs = node.ChildNodes().OfType<ArgumentListSyntax>().First();

                    var args = new List<QueryArgument>();
                    {
                        var typeList = new List<IdentifierNameSyntax>();
                        GetNodes(node, typeList);
                        if (typeList.Count == 0)
                        {
                            throw new InvalidOperationException("no  type found");
                        }

                        var t = typeList[0];
                        var typeName = t.GetFirstToken().Text;
                        
                        var typeSymbolInfo = Compilation.GetSemanticModel(tree, true).GetSymbolInfo(t);
                        var typeSymbol =
                            (typeSymbolInfo.Symbol ?? typeSymbolInfo.CandidateSymbols.FirstOrDefault()) as
                            INamedTypeSymbol;
                        if (typeSymbol == null)
                            throw new InvalidOperationException($"Type '{typeName}' not found");
                        
                        args.Add(new QueryArgument
                        {
                            Symbol = typeSymbol,
                            PublicName = "SingletonValue",
                            CanWrite = false, // assume that we can't write to resources for now (if they want to write, they just need to do it via queries)
                            IsOption = false,
                            Modifier = QueryArgument.EModifier.All,
                            Custom = ComponentGenerator.GetCustomAccess(Compilation, typeSymbol)
                        });
                    }

                    var source = new QuerySource(
                        (INamedTypeSymbol) symbol,
                        symbol.ContainingNamespace?.ToString(),
                        $"res{i}",
                        tree.FilePath,
                        args.ToArray(),
                        StructureName: $"__Resource{i}",
                        ValidStructureName: $"__Resource{i}",
                        AlreadyGenerated: false
                    );
                    arguments.Add($"res{i}", new SystemQuery
                    {
                        IsOptional = identifier.StartsWith("Optional"),
                        IsResource = true,
                        Source = source
                    });

                    Replace(node,
                        SyntaxFactory.ParseExpression($"res{i}.First().SingletonValue")
                    );
                }
            }

            // get all commands
            {
                var options = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                foreach (var inter in symbol.Interfaces)
                {
                    // not a command, skip
                    if (!inter.AllInterfaces.Any(i => i.Name == "IRevolutionCommand"))
                        continue;

                    options.Add(inter);
                }

                arguments.Add("Cmd", new CommandSource(
                    (INamedTypeSymbol) symbol,
                    symbol.ContainingNamespace?.ToString(),
                    "cmd",
                    tree.FilePath,
                    options.ToArray(),
                    StructureName: "__Commands",
                    ValidStructureName: "__Commands",
                    AlreadyGenerated: false
                ));
            }

            foreach (var (_, obj) in arguments)
            {
                if (obj is SystemQuery systemQuery)
                    QueryGenerator.GenerateQuery(systemQuery.Source);
                if (obj is CommandSource {AlreadyGenerated: false} commandSource)
                    CommandGenerator.GenerateCommand(commandSource);
            }

            bodySyntax = bodySyntax.ReplaceNodes(replace.Keys, (o, r) => replace[o]);
        }

        var systemName = $"{symbol.Name}";

        var needConstructor = false;

        void BeginSystem()
        {
            sb.AppendLine($"public partial struct {systemName}\n{{");
            sb.AppendLine($"    public partial struct __InternalSystem : ISystem\n    {{");
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
            sb.AppendLine($"        public {systemName} Payload;");
            
            foreach (var (name, obj) in arguments)
            {
                if (obj is SystemQuery querySource)
                    sb.AppendLine($"        private {querySource.Source.GetDisplayName()} {name};");

                if (obj is CommandSource commandSource)
                    sb.AppendLine($"        private {commandSource.GetDisplayName()} {name};");
            }
        }

        void CreateFunction()
        {
            var init = new StringBuilder();
            foreach (var (name, obj) in arguments)
            {
                if (obj is SystemQuery query)
                {
                    init.Append($"            {name} = new {query.Source.GetDisplayName()}(world);");
                }
                
                if (obj is CommandSource cmd)
                {
                    init.Append($"            {name} = new {cmd.GetDisplayName()}(world);");
                }
                
                init.AppendLine();
            }
            
            sb.AppendLine(@$"
        public bool Create(SystemHandle systemHandle, RevolutionWorld world) 
        {{
            world.SetSystem<{systemName}>(systemHandle);

            // This is done so that the initial method doesn't appear as unused
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
            var constraintsMethod = symbol.GetMembers("Constraints")[0] as IMethodSymbol;
            sb.AppendLine(@$"
        public void PreQueue(SystemHandle systemHandle, RevolutionWorld world) 
        {{
            var {constraintsMethod.Parameters[0].Name} = new SystemObject
            {{
                World = world,
                Handle = systemHandle,
                DependenciesType = _systemDependenciesType
            }};

{(constraintsMethod.DeclaringSyntaxReferences[0].GetSyntax() as MethodDeclarationSyntax)!.Body }
        }}
");     
        }

        void QueueFunction()
        {
            var fromSource = new StringBuilder();
            foreach (var (name, obj) in arguments)
            {
                if (obj is SystemQuery or CommandSource)
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
                if (obj is SystemQuery querySource)
                {
                    fieldFromSource.Append("            ");
                    fieldFromSource.AppendLine($"public {querySource.Source.GetDisplayName()} {name};");
                }
                
                if (obj is CommandSource commandSource)
                {
                    fieldFromSource.Append("            ");
                    fieldFromSource.AppendLine($"public {commandSource.GetDisplayName()} {name};");
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
                if (obj is SystemQuery or CommandSource)
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
                if (obj is SystemQuery querySource)
                {
                    if (querySource.IsOptional)
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

                using (SwapDependency.BeginContext())
                    return {canExecute};
            }}

            private void Do()
            {{
{bodySyntax.Body}
            }}

{additional}
        }}
");
        }

        void AnonymousFunctions()
        {
            sb.AppendLine($@"
    public void AddToSystemGroup(SystemGroup group) {{
        group.Add(new __InternalSystem() {{ Payload = this }});
    }}

    private dynamic RequiredQuery(params object[] p) => throw new NotImplementedException();
    private dynamic OptionalQuery(params object[] p) => throw new NotImplementedException();
    private T RequiredResource<T>() => throw new NotImplementedException();
    private T OptionalResource<T>() => throw new NotImplementedException();

    private struct __All<T> {{}}
    private struct __Or<T> {{}}
    private struct __None<T> {{}}

    private __All<T> Write<T>(string name = """") => throw new NotImplementedException();
    private __All<T> Read<T>(string name = """") => throw new NotImplementedException();
    private __All<T> All<T>(string name = """") => throw new NotImplementedException();
    private __Or<T> Or<T>(string name = """") => throw new NotImplementedException();
    private __None<T> None<T>(string name = """") => throw new NotImplementedException();
");

            foreach (var arg in arguments)
            {
                if (arg.Value is SystemQuery {IsResource: false, Source: { } q})
                {
                    var paramSb = new StringBuilder();
                    for (var index = 0; index < q.Arguments.Length; index++)
                    {
                        paramSb.Append($"__{q.Arguments[index].Modifier}<{q.Arguments[index].Symbol.Name}> _{index}");
                        if (index + 1 < q.Arguments.Length)
                            paramSb.Append(", ");
                    }

                    sb.AppendLine($@"
    private {q.StructureName} RequiredQuery({paramSb}) => throw new NotImplementedException();
");
                    sb.AppendLine($@"
    private {q.StructureName} OptionalQuery({paramSb}) => throw new NotImplementedException();
");
                }

                if (arg.Key == "Cmd")
                {
                    sb.AppendLine($@"
    private __Commands Cmd;
");
                }
            }
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
                
                AnonymousFunctions();
                
                sb.AppendLine("}");
            }
            EndParentTypes();
        }

        var fileName = $"{(symbol.ContainingType == null ? "" : $"{symbol.ContainingType.Name}.")}{symbol.Name}";
        Log(0, fileName);
        Context.AddSource($"SYSTEM.{Path.GetFileNameWithoutExtension(tree.FilePath)}.{fileName}", "#pragma warning disable\n" + sb.ToString());
    }
}