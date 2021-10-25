using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace revecs.Generator;

[Generator]
public class RevolutionGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(ReceiveSyntax);
    }

    public void Execute(GeneratorExecutionContext context)
    {
        // retrieve the populated receiver
        if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
            return;

        var sw = new Stopwatch();
        void start() => sw.Restart();

        void stop(string name)
        {
            sw.Stop();
            receiver.Log.Add($"Elapsed ({name}) " + sw.Elapsed.TotalMilliseconds + "ms");
        }

        try
        {
            var compilation = context.Compilation;

            start();
            var comp = new ComponentGenerator(context, receiver, ref compilation);
            {
                stop("generating component");
                
                start();
                var trees = new List<(string, SyntaxTree)>();
                foreach (var (fileName, str) in comp.FinalMap)
                {
                    trees.Add((fileName, CSharpSyntaxTree.ParseText(str, context.ParseOptions as CSharpParseOptions)));
                }
                stop("parsing component trees");

                // Used to mostly inject generated commands from components
                // If this is not done, then the type will have 0 fields. (info such as Body, Init will not be present)
                //
                // DON'T REMOVE (or fix if it break)
                start();
                compilation = compilation.AddSyntaxTrees(trees.Select(tuple => tuple.Item2));
                stop("adding trees to compilation");
            }
            
            start();
            var query = new QueryGenerator(context, receiver, ref compilation);
            stop("generating queries");
            
            start();
            var cmd = new CommandGenerator(context, receiver, ref compilation);
            stop("generating commands");
            
            start();
            _ = new SystemGenerator(query, cmd, comp, context, receiver, ref compilation);
            stop("generating systems");
        }
        catch (Exception ex)
        {
            receiver.Log.Add(ex.ToString());
        }

        context.AddSource("Logs",
            SourceText.From(
                $@"/*{Environment.NewLine + string.Join(Environment.NewLine, receiver.Log) + Environment.NewLine}*/",
                Encoding.UTF8));
    }

    private ISyntaxContextReceiver ReceiveSyntax()
    {
        return new SyntaxReceiver();
    }
}