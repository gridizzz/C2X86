using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using JsToCSharp.Domain;

namespace JsToCSharp.Desktop;

internal static class EditorHighlighting
{
    private static readonly Lazy<IReadOnlyDictionary<Language, IHighlightingDefinition>> Definitions = new(CreateDefinitions);

    public static IHighlightingDefinition Get(Language language) => Definitions.Value[language];

    private static IReadOnlyDictionary<Language, IHighlightingDefinition> CreateDefinitions()
    {
        var manager = HighlightingManager.Instance;
        var definitions = new Dictionary<Language, IHighlightingDefinition>
        {
            [Language.JavaScript] = manager.GetDefinition("JavaScript"),
            [Language.CSharp] = manager.GetDefinition("C#"),
            [Language.Python] = manager.GetDefinition("Python"),
            [Language.Java] = manager.GetDefinition("Java"),
            // AvaloniaEdit ships a shared C/C++ lexical definition.
            [Language.C] = manager.GetDefinition("C++"),
            [Language.Cpp] = manager.GetDefinition("C++")
        };
        foreach (var language in new[] { Language.TypeScript, Language.Go, Language.Rust })
        {
            using var stream = typeof(EditorHighlighting).Assembly.GetManifestResourceStream(
                $"JsToCSharp.Desktop.Highlighting.{language}.xshd")
                ?? throw new InvalidOperationException($"Missing highlighting definition for {language}.");
            using var reader = XmlReader.Create(stream);
            definitions[language] = HighlightingLoader.Load(reader, manager);
        }
        foreach (var language in new[] { Language.PHP, Language.Kotlin, Language.Swift, Language.Ruby })
        {
            using var stream = typeof(EditorHighlighting).Assembly.GetManifestResourceStream(
                $"JsToCSharp.Desktop.Highlighting.{language}.xshd")
                ?? throw new InvalidOperationException($"Missing highlighting definition for {language}.");
            using var reader = XmlReader.Create(stream);
            definitions[language] = HighlightingLoader.Load(reader, manager);
        }

        foreach (var definition in definitions.Values.Distinct())
            ApplyDarkPlusPalette(definition);
        return definitions;
    }

    private static void ApplyDarkPlusPalette(IHighlightingDefinition definition)
    {
        // VS Code Dark+ syntax colors, applied to the shared categories used by
        // AvaloniaEdit's bundled language definitions.
        var colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Keyword"] = "#569CD6",
            ["Comment"] = "#6A9955",
            ["String"] = "#CE9178",
            ["Char"] = "#CE9178",
            ["Number"] = "#B5CEA8",
            ["Type"] = "#4EC9B0",
            ["Class"] = "#4EC9B0",
            ["Interface"] = "#4EC9B0",
            ["MethodCall"] = "#DCDCAA",
            ["Function"] = "#DCDCAA",
            ["Property"] = "#9CDCFE",
            ["LocalVariable"] = "#9CDCFE",
            ["Parameter"] = "#9CDCFE",
            ["Preprocessor"] = "#C586C0",
            ["XmlDoc"] = "#608B4E",
            ["Punctuation"] = "#D4D4D4",
            ["Operator"] = "#D4D4D4"
        };

        foreach (var color in definition.NamedHighlightingColors)
        {
            if (color.Name is { } name && colors.TryGetValue(name, out var hex))
                color.Foreground = new SimpleHighlightingBrush(Avalonia.Media.Color.Parse(hex));
        }
    }
}
