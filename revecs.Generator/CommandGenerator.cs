using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace revecs.Generator;

public record CommandSource
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

public class CommandGenerator
{
    public readonly GeneratorExecutionContext Context;
    public readonly SyntaxReceiver Receiver;

    public Compilation Compilation;
    
    public CommandGenerator(GeneratorExecutionContext context, SyntaxReceiver syntaxReceiver,
        ref Compilation compilation)
    {
        Context = context;
        Receiver = syntaxReceiver;

        Compilation = compilation;
        
        GenerateHelpers();
        CreateCommands();

        compilation = Compilation;
    }

    private void Log<T>(int indent, T txt)
    {
        Receiver.Log.Add(string.Join(null, Enumerable.Repeat("\t", indent)) + txt);
    }

    private void GenerateHelpers()
    {
        const string helpersSource =
            @"
using System;
using System.Runtime.CompilerServices;
namespace revecs
{
    internal class CmdAttribute : Attribute { }
}
";
        Context.AddSource("CommandAttributes", helpersSource);
    }
    
    private static IEnumerable<INamedTypeSymbol> GetParentTypes(INamedTypeSymbol? type)
    {
        while (type != null)
        {
            yield return type;
            type = type.ContainingType;
        }
    }

    record Constants(string? Imports, string? Body, string? Variables, string? Init, string? Dependencies, bool Write, string? Readers);

    public void GenerateCommand(CommandSource source)
    {
        var sb = new StringBuilder();
        var map = new Dictionary<string, Constants>();

        void CollectConstants()
        {
            void FindInterface(INamedTypeSymbol symbol)
            {
                var constant = new Constants(null, null, null, null, null, false, null);
                foreach (var member in symbol.GetMembers())
                {
                    Log(2, member.Name + " : " + member.GetType());
                    
                    if (member is IFieldSymbol fieldSymbol)
                    {
                        if (!fieldSymbol.HasConstantValue)
                            continue;
                        
                        constant = fieldSymbol.Name switch
                        {
                            "Imports" => constant with {Imports = fieldSymbol.ConstantValue.ToString()},
                            "Variables" => constant with {Variables = fieldSymbol.ConstantValue.ToString()},
                            "Init" => constant with {Init = fieldSymbol.ConstantValue.ToString()},
                            "Body" => constant with {Body = fieldSymbol.ConstantValue.ToString()},
                            "Dependencies" => constant with {Dependencies = fieldSymbol.ConstantValue.ToString()},
                            "WriteAccess" => constant with {Write = true},
                            "ReadAccess" => constant with {Readers = fieldSymbol.ConstantValue.ToString()},
                            _ => constant
                        };
                    }
                }

                foreach (var child in symbol.Interfaces)
                {
                    Log(3, "Child Interface: " + child.GetTypeName());
                    FindInterface(child);
                }
                
                map[symbol.GetTypeName()] = constant;
            }
            
            foreach (var header in source.Header)
            {
                Log(1, header.Name);
                FindInterface(header);
            }
        }
        
        var parentTypes = GetParentTypes(source.Parent)
            .Reverse()
            .ToList();
        
        void Usings()
        {
            sb.Append(@"using revecs.Core;
using revecs.Utility;
using revecs.Querying;
using revecs.Systems;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.ComponentModel;
using revtask.Core;
using revtask.Helpers;

");
        }

        void BeginNamespace()
        {
            if (source.Namespace is { } ns)
                sb.AppendFormat("namespace {0}\n{{\n", ns);
        }

        void EndNamespace()
        {
            if (source.Namespace is { })
                sb.AppendFormat("}}\n");
        }
        
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

        void BeginCommand()
        {
            var structName = source.StructureName ?? source.Name;

            var interfaces = source.Header.Select(t => t.GetTypeName());
            if (structName.StartsWith("__"))
                sb.Append("    [EditorBrowsable(EditorBrowsableState.Never)]");

            if (interfaces.Any())
                sb.AppendLine(
                    $"    partial struct {structName} :\n{string.Join(",\n            ", interfaces)}\n    {{"
                );
            else
            {
                sb.AppendLine(
                    $"    partial struct {structName}\n    {{"
                );
            }
        }

        void EndCommand()
        {
            sb.AppendLine("    }");
        }

        var addDependencyReader = false;
        void Variables()
        {
            var expr = new StringBuilder();

            foreach (var (name, obj) in map)
            {
                if (obj.Imports is { } imports)
                {
                    sb.Insert(0, imports);
                }
            }
            
            foreach (var (name, obj) in map)
            {
                if (obj.Readers != null)
                    addDependencyReader = true;

                if (obj.Variables is not { } vars)
                    continue;

                expr.AppendLine($"        // {name}");
                expr.AppendLine(vars);
            }

            sb.AppendLine($"        public const bool RequireDependencyReader = {addDependencyReader.ToString().ToLower()};");

            sb.AppendLine(expr.ToString());
        }

        void Init()
        {
            var expr = new StringBuilder();
            foreach (var (name, obj) in map)
            {
                if (obj.Init is not { } init)
                    continue;
                
                expr.AppendLine($"        // {name}");
                expr.AppendLine("        {");
                expr.AppendLine(init);
                expr.AppendLine("        }");
            }
            
            sb.AppendLine($@"        public readonly RevolutionWorld World;

        public {source.StructureName ?? source.Name}(RevolutionWorld world)
        {{
            World = world;

{expr}
        }}
");
        }

        void Body()
        {
            var expr = new StringBuilder();
            foreach (var (name, obj) in map)
            {
                if (obj.Body is not { } body)
                    continue;
                
                expr.AppendLine($"        // {name}");
                expr.AppendLine(body);
            }

            sb.AppendLine(expr.ToString());
        }

        void Dependency()
        {
            // Swaps and Completes
            var expr = new StringBuilder();
            foreach (var (name, obj) in map)
            {
                if (obj.Dependencies is not { } dep)
                    continue;

                expr.AppendLine($"        // {name}");
                expr.AppendLine($"            && ({dep})");
            }

            sb.AppendLine($@"
        public bool DependencySwap(IJobRunner runner, JobRequest request)
        {{
            using (SwapDependency.BeginContext())
            return true 
{expr};
        }}
");
            // Readers
            expr.Clear();
            foreach (var (name, obj) in map)
            {
                if (obj.Readers is not { } dep)
                    continue;

                expr.AppendLine($"            // {name}");
                expr.AppendLine(dep);
            }

            sb.AppendLine($@"
        public void AddDependencyReader(IJobRunner runner, JobRequest request)
        {{
{expr}
        }}
");
        }

        CollectConstants();
        Usings();
        BeginNamespace();
        {
            BeginParentTypes();
            {
                BeginCommand();
                {
                    Variables();
                    Init();
                    Body();
                    Dependency();
                }
                EndCommand();
            }
            EndParentTypes();
        }
        EndNamespace();
        
        var fileName = $"{(source.Parent == null ? "" : $"{source.Parent.Name}.")}{source.Name}";
        Context.AddSource($"COMMAND.{Path.GetFileNameWithoutExtension(source.FilePath)}.{fileName}", "#pragma warning disable\n" + sb.ToString());
    }
    
    private void CreateCommands()
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

                var symbol = (INamedTypeSymbol) semanticModel.GetDeclaredSymbol(declaredStruct);

                // is system (cancel if found)
                var systemInterface = symbol!.AllInterfaces.FirstOrDefault(i => i.Name == "IRevolutionSystem");
                // is command
                var commandInterface = symbol!.AllInterfaces.FirstOrDefault(i => i.Name == "IRevolutionCommand");
                if (systemInterface == null && commandInterface != null)
                {
                    Log(0, "Found Command: " + symbol.GetTypeName() + ", File: " + tree.FilePath);
                    
                    var source = new CommandSource
                    (
                        symbol.ContainingType,
                        symbol.ContainingNamespace?.ToString(),
                        symbol.Name,
                        tree.FilePath,
                        symbol.Interfaces.Select(t =>
                        {
                            return (INamedTypeSymbol) t;
                        }).ToArray(),
                        IsRecord: symbol.IsRecord
                    );
                    
                    GenerateCommand(source);
                }
            }
        }
    }
}