using System.Text;
using Xunit;
using Xunit.Abstractions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace revecs.Tests;
           
public class TestBase : IDisposable
{
    protected ITestOutputHelper output;
    
    class Writer : TextWriter
    {
        public override Encoding Encoding { get; }

        public Action<string> WriteEvent;

        public override void WriteLine(string? value) => WriteEvent(value ?? ":null:");
        public override void Write(string? value) => WriteEvent(value ?? ":null:");
    }

    private Writer _writer;

    public TestBase(ITestOutputHelper output)
    {
        this.output = output;

        _writer = new Writer() {WriteEvent = output.WriteLine};
        Console.SetOut(_writer);
    }

    public void Dispose()
    {
        if (Console.Out == _writer)
            Console.SetOut(TextWriter.Null);
    }
}