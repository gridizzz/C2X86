using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using JsToCSharp.Desktop;
using JsToCSharp.Domain;
using Xunit;

namespace JsToCSharp.Desktop.Tests;

public sealed class EditorHighlightingTests
{
    [Theory]
    [InlineData(Language.JavaScript, "function example() {}")]
    [InlineData(Language.CSharp, "public class Example {}")]
    [InlineData(Language.Python, "def example(): pass")]
    [InlineData(Language.TypeScript, "interface Example {}")]
    [InlineData(Language.Java, "public class Main {}")]
    [InlineData(Language.Go, "func main() {}")]
    [InlineData(Language.Rust, "fn main() {}")]
    [InlineData(Language.C, "int main(void) {}")]
    [InlineData(Language.Cpp, "class Example {};")]
    [InlineData(Language.PHP, "<?php function example() {}")]
    [InlineData(Language.Kotlin, "fun example() = 1")]
    [InlineData(Language.Swift, "func example() -> Int { 1 }")]
    [InlineData(Language.Ruby, "def example; end")]
    public void EachLanguageLoadsAndHighlightsKeywords(Language language, string code)
    {
        var definition = EditorHighlighting.Get(language);
        Assert.NotNull(definition);
        using var highlighter = new DocumentHighlighter(new TextDocument(code), definition);
        Assert.Contains(highlighter.HighlightLine(1).Sections, section => section.Length > 0);
    }

    [Theory]
    [InlineData(Language.Go, "`line one\nline two`\nfunc main() {}")]
    [InlineData(Language.TypeScript, "`line one\nline two`\ninterface Example {}")]
    [InlineData(Language.Rust, "/* line one\nline two */\nfn main() {}")]
    public void MultilineSpansEndBeforeNextKeyword(Language language, string code)
    {
        var document = new TextDocument(code);
        using var highlighter = new DocumentHighlighter(document, EditorHighlighting.Get(language));
        Assert.Contains(highlighter.HighlightLine(2).Sections, section => section.Color.Name is "String" or "Comment");
        Assert.Contains(highlighter.HighlightLine(3).Sections,
            section => section.Offset == document.GetLineByNumber(3).Offset && section.Color.Name == "Keyword");
    }
}
