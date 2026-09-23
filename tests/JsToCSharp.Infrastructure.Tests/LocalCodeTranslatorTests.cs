using JsToCSharp.Infrastructure;
using Xunit;

namespace JsToCSharp.Infrastructure.Tests;

public sealed class LocalCodeTranslatorTests
{
    [Theory]
    [InlineData("<think>reasoning</think>\n```csharp\nint x = 1;\n```", "int x = 1;")]
    [InlineData("\n</think>\nlet x = 1;", "let x = 1;")]
    [InlineData("const s = `literal`;", "const s = `literal`;")]
    public void RemovesOnlyOutputWrappers(string output, string expected)
        => Assert.Equal(expected, LocalCodeTranslator.CleanOutput(output));

    [Theory]
    [InlineData("")]
    [InlineData("<think>unfinished reasoning")]
    [InlineData("<think>done</think>")]
    public void RejectsMissingCode(string output)
        => Assert.Throws<InvalidOperationException>(() => LocalCodeTranslator.CleanOutput(output));

    [Theory]
    [InlineData("GGUF", true)]
    [InlineData("{}", false)]
    [InlineData("xxxx", false)]
    public void ChecksHeaderWithoutRequiringExtension(string content, bool valid)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, content);
            if (valid) LocalCodeTranslator.ValidateModelFile(path);
            else Assert.Throws<InvalidOperationException>(() => LocalCodeTranslator.ValidateModelFile(path));
        }
        finally { File.Delete(path); }
    }
}
