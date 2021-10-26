using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace revecs.Generator;

public record ComponentSource(
    INamedTypeSymbol? Parent,
    string? Namespace,
    string Name,
    string FilePath,
    INamedTypeSymbol[] Header,
    bool IsRecord = false
);

public class ComponentGenerator
{
    public readonly GeneratorExecutionContext Context;
    public readonly SyntaxReceiver Receiver;

    public Dictionary<string, string> FinalMap = new();

    public Compilation compilation;

    public ComponentGenerator(GeneratorExecutionContext context, SyntaxReceiver syntaxReceiver,
        ref Compilation compilation)
    {
        Context = context;
        Receiver = syntaxReceiver;

        this.compilation = compilation;

        GenerateHelpers();
        CreateComponents();

        compilation = this.compilation;
    }

    public record AccessorAccess(string FieldType, string Init, string Access, string ValueType)
    {
        public string GetInit(string fieldName, string componentType, string worldName)
        {
            return Init
                .Replace("[world]", worldName, StringComparison.InvariantCultureIgnoreCase)
                .Replace("[field]", fieldName, StringComparison.InvariantCultureIgnoreCase)
                .Replace("[componentType]", componentType, StringComparison.InvariantCultureIgnoreCase);
        }

        public string GetAccess(string valueName, string fieldName, string entityName, string access)
        {
            return Access
                .Replace("[value]", valueName, StringComparison.InvariantCultureIgnoreCase)
                .Replace("[field]", fieldName, StringComparison.InvariantCultureIgnoreCase)
                .Replace("[access]", access, StringComparison.InvariantCultureIgnoreCase)
                .Replace("[entity]", entityName, StringComparison.InvariantCultureIgnoreCase);
        }
    }

    public record WorldAccess(string Access, string ValueType)
    {
        public string GetAccess(string valueName, string worldName, string entityName, string componentType,
            string access)
        {
            return Access
                .Replace("[value]", valueName, StringComparison.InvariantCultureIgnoreCase)
                .Replace("[world]", worldName, StringComparison.InvariantCultureIgnoreCase)
                .Replace("[access]", access, StringComparison.InvariantCultureIgnoreCase)
                .Replace("[componentType]", componentType, StringComparison.InvariantCultureIgnoreCase)
                .Replace("[entity]", entityName, StringComparison.InvariantCultureIgnoreCase);
        }
    }

    public record CustomAccessResult(bool DisableReferenceWrapper, string Usings, AccessorAccess? ViaAccessor = null,
        WorldAccess? ViaWorld = null);

    public static CustomAccessResult GetCustomAccess(Compilation compilation, INamedTypeSymbol symbol)
    {
        var type = compilation.GetTypeByMetadataName(
            symbol.GetTypeName().Replace("global::", string.Empty)
            + "+Type"
        );
        if (type == null)
        {
            var target = $"{symbol.GetTypeName()}.Type";
            foreach (var tree in compilation.SyntaxTrees)
            {
                var model = compilation.GetSemanticModel(tree, true);

                foreach (var declaredType in tree.GetRoot()
                             .DescendantNodesAndSelf()
                             .OfType<TypeDeclarationSyntax>())
                {
                    var typeSymbol = (INamedTypeSymbol) model.GetDeclaredSymbol(declaredType);
                    if (typeSymbol.GetTypeName() == target)
                    {
                        type = typeSymbol;
                        break;
                    }
                }

                if (type != null)
                    break;
            }
        }

        if (type == null)
        {
            var str = string.Empty;
            foreach (var tree in compilation.SyntaxTrees)
            {
                var model = compilation.GetSemanticModel(tree);

                foreach (var declare in tree.GetRoot()
                             .DescendantNodesAndSelf()
                             .OfType<TypeDeclarationSyntax>())
                {
                    str += $"\n{((INamedTypeSymbol) model.GetDeclaredSymbol(declare)).GetTypeName()}";
                }
            }

            throw new InvalidOperationException($"{symbol.GetTypeName()} has no nested 'Type' class; {str}");
        }

        var disableReferenceWrapper = false;
        var usings = string.Empty;
        string accessorFieldType = null,
            accessorInit = null,
            accessorAccess = null,
            accessorValueType = symbol.GetTypeName();
        string worldAccess = null,
            worldValueType = symbol.GetTypeName();

        foreach (var member in symbol.GetMembers())
        {
            if (member is IFieldSymbol fieldSymbol)
            {
                if (!fieldSymbol.IsConst)
                    continue;

                if (fieldSymbol.Name == "Imports")
                    usings += fieldSymbol.ConstantValue!.ToString();
            }
        }

        foreach (var member in type.GetMembers())
        {
            if (member is IFieldSymbol fieldSymbol)
            {
                if (!fieldSymbol.IsConst)
                    continue;

                if (fieldSymbol.Name == "DisableReferenceWrapper")
                    disableReferenceWrapper = (bool) fieldSymbol.ConstantValue!;

                if (fieldSymbol.Name == "Imports")
                    usings += fieldSymbol.ConstantValue!.ToString();

                if (fieldSymbol.Name == "AccessorAccess_FieldType")
                    accessorFieldType = fieldSymbol.ConstantValue!.ToString()!;
                if (fieldSymbol.Name == "AccessorAccess_Init")
                    accessorInit = fieldSymbol.ConstantValue!.ToString()!;
                if (fieldSymbol.Name == "AccessorAccess_Access")
                    accessorAccess = fieldSymbol.ConstantValue!.ToString()!;
                if (fieldSymbol.Name == "AccessorAccess_ValueType")
                    accessorValueType = fieldSymbol.ConstantValue!.ToString()!;
                if (fieldSymbol.Name == "WorldAccess_Access")
                    worldAccess = fieldSymbol.ConstantValue!.ToString()!;
                if (fieldSymbol.Name == "WorldAccess_ValueType")
                    worldValueType = fieldSymbol.ConstantValue!.ToString()!;
            }
        }

        var accessor = accessorFieldType == null || accessorInit == null || accessorAccess == null
            ? null
            : new AccessorAccess(accessorFieldType, accessorInit, accessorAccess, accessorValueType);
        var world = worldAccess == null
            ? null
            : new WorldAccess(worldAccess, worldValueType);

        return new CustomAccessResult(disableReferenceWrapper, usings, accessor, world);
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
}
";
        Context.AddSource("ComponentAttributes", helpersSource);
    }

    private static IEnumerable<INamedTypeSymbol> GetParentTypes(INamedTypeSymbol? type)
    {
        while (type != null)
        {
            yield return type;
            type = type.ContainingType;
        }
    }

    record Constants(string? Body, string? Imports);

    public void GenerateComponent(ComponentSource source)
    {
        var sb = new StringBuilder();
        var map = new Dictionary<string, Constants>();

        void CollectConstants()
        {
            void FindInterface(INamedTypeSymbol symbol)
            {
                var constant = new Constants(null, null);
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
                            "Body" => constant with {Body = fieldSymbol.ConstantValue.ToString()},
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
using revecs.Query;
using revecs.Systems;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revtask.Core;
using revtask.Helpers;
");

            foreach (var (_, obj) in map)
            {
                if (obj.Imports != null)
                {
                    sb.AppendLine(obj.Imports);
                }
            }

            sb.AppendLine();
        }

        void BeginNamespace()
        {
            if (source.Namespace != null)
                sb.AppendFormat("namespace {0}\n{{\n", source.Namespace);
        }

        void EndNamespace()
        {
            if (source.Namespace != null)
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
            foreach (var _ in parentTypes) sb.AppendLine("}");
        }

        void BeginComponent()
        {
            sb.AppendLine($"    partial {(source.IsRecord ? "record" : "")} struct {source.Name} {{");
        }

        void EndComponent()
        {
            sb.AppendLine("    }");
        }

        void Body()
        {
            var typeAddr = string.Empty;
            if (source.Parent != null)
                typeAddr = $"{source.Parent.GetTypeName()}.";
            else if (source.Namespace != null)
                typeAddr = $"global::{source.Namespace}.";

            typeAddr += source.Name;

            var expr = new StringBuilder();
            foreach (var (name, obj) in map)
            {
                if (obj.Body is not { } body)
                    continue;

                expr.AppendLine($"        // {name}");
                expr.AppendLine(body
                    .Replace("[Type]", source.Name)
                    .Replace("[TypeAddr]", typeAddr)
                );
            }

            sb.AppendLine(expr.ToString());
        }

        CollectConstants();
        Usings();
        BeginNamespace();
        {
            BeginParentTypes();
            {
                BeginComponent();
                {
                    Body();
                }
                EndComponent();
            }
            EndParentTypes();
        }
        EndNamespace();

        var fileName = $"{(source.Parent == null ? "" : $"{source.Parent.Name}.")}{source.Name}";
        FinalMap[fileName] = sb.ToString();

        Context.AddSource($"{Path.GetFileNameWithoutExtension(source.FilePath)}.{fileName}",
            "#pragma warning disable\n" + sb.ToString());
    }

    private void CreateComponents()
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            foreach (var declaredStruct in tree
                         .GetRoot()
                         .DescendantNodesAndSelf()
                         .OfType<TypeDeclarationSyntax>())
            {
                if (tree.FilePath.Contains("Generated/Generator"))
                    continue;

                var symbol = (INamedTypeSymbol) semanticModel.GetDeclaredSymbol(declaredStruct);

                // is query
                var componentInterface = symbol!.AllInterfaces.FirstOrDefault(i => i.Name == "IRevolutionComponent");
                if (componentInterface != null)
                {
                    Log(0, "Found Component: " + symbol.GetTypeName() + ", File: " + tree.FilePath);
                    
                    var source = new ComponentSource
                    (
                        symbol.ContainingType,
                        symbol.ContainingNamespace?.ToString(),
                        symbol.Name,
                        tree.FilePath,
                        symbol.Interfaces.Select(t => { return (INamedTypeSymbol) t; }).ToArray(),
                        IsRecord: symbol.IsRecord
                    );

                    GenerateComponent(source);
                }
            }
        }
    }
}