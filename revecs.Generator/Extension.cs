using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace revecs.Generator;

public static class Extension
{
    public static string GetTypeName(this ITypeSymbol type)
    {
        return (type.TypeKind is TypeKind.TypeParameter || type.SpecialType is > 0 and <= SpecialType.System_String
            ? type.ToString()
            : $"global::{type}")!;
    }

    public static INamedTypeSymbol? GetUnknownType(this Compilation compilation, string name)
    {
        var target = name;
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree, true);

            foreach (var declaredType in tree.GetRoot()
                         .DescendantNodesAndSelf()
                         .OfType<TypeDeclarationSyntax>())
            {
                var typeSymbol = (INamedTypeSymbol) model.GetDeclaredSymbol(declaredType);
                if (typeSymbol!.GetTypeName() == target)
                {
                    return typeSymbol;
                }
            }
        }

        return null;
    }


    // this is ugly as heck
    //
    // but what it does is finding the method from a nameof() expression.
    // it check in the syntax tree so it should find the right symbol.
    //
    // perhaps make it simpler later
    public static IMethodSymbol? GetMethodSymbolFromNameOf(this MethodDeclarationSyntax declare,
        SemanticModel semanticModel, int pos)
    {
        foreach (var attr in declare.DescendantNodes()
                     .OfType<AttributeSyntax>())
        {
            if (pos-- > 0)
                continue;

            foreach (var identifier in attr.DescendantNodes(_ => true)
                         .OfType<IdentifierNameSyntax>())
            {
                var symbolInfo = semanticModel.GetSymbolInfo(identifier);
                var finalSymbol = symbolInfo.Symbol;

                if (finalSymbol == null)
                {
                    foreach (var candidate in symbolInfo.CandidateSymbols)
                    {
                        // BINGO
                        if (candidate is IMethodSymbol methodSymbol)
                        {
                            return methodSymbol;
                        }
                    }
                }
            }

            /*foreach (var list in attr.DescendantNodes()
                         .OfType<AttributeArgumentListSyntax>())
            {
                foreach (var arg in list.DescendantNodes()
                             .OfType<AttributeArgumentSyntax>())
                {
                    foreach (var expr in arg.DescendantNodes()
                                 .OfType<InvocationExpressionSyntax>())
                    {
                        foreach (var argList in expr.DescendantNodes()
                                     .OfType<ArgumentListSyntax>())
                        {
                            foreach (var exprArg in argList.DescendantNodes()
                                         .OfType<ArgumentSyntax>())
                            {
                                foreach (var identifier in exprArg.DescendantNodes()
                                             .OfType<IdentifierNameSyntax>())
                                {
                                    var symbolInfo = semanticModel.GetSymbolInfo(identifier);
                                    var finalSymbol = symbolInfo.Symbol;

                                    if (finalSymbol == null)
                                    {
                                        foreach (var candidate in symbolInfo.CandidateSymbols)
                                        {
                                            // BINGO
                                            if (candidate is IMethodSymbol methodSymbol)
                                            {
                                                return methodSymbol;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }*/
        }

        return null;
    }
}