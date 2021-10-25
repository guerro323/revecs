using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace revecs.Generator;

[Generator]
public class SyntaxReceiver : ISyntaxContextReceiver
{
    public List<string> Log = new();

    public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
    {
        try
        {
            if (context.Node is ClassDeclarationSyntax classDeclarationSyntax)
            {
                var testClass = (INamedTypeSymbol) context.SemanticModel.GetDeclaredSymbol(context.Node)!;
                Log.Add($"Found a class named {testClass.Name}");
            }
        }
        catch (Exception ex)
        {
            Log.Add("Error parsing syntax: " + ex);
        }
    }
}