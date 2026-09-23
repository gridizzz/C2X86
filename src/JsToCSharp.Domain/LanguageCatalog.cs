namespace JsToCSharp.Domain;

public sealed record LanguageInfo(Language Language, string DisplayName, string FileExtension,
    string SuggestedFileName, IReadOnlyList<string> SourceExtensions)
{
    public override string ToString() => DisplayName;
}

public static class LanguageCatalog
{
    public static IReadOnlyList<LanguageInfo> All { get; } = Array.AsReadOnly(new[]
    {
        Create(Language.JavaScript, "JavaScript", ".js", "translated-code.js", ".js", ".mjs", ".cjs", ".jsx"),
        Create(Language.CSharp, "C#", ".cs", "TranslatedCode.cs", ".cs"),
        Create(Language.Python, "Python", ".py", "translated_code.py", ".py", ".pyw"),
        Create(Language.TypeScript, "TypeScript", ".ts", "translated-code.ts", ".ts", ".tsx", ".mts", ".cts"),
        Create(Language.Java, "Java", ".java", "Main.java", ".java"),
        Create(Language.Go, "Go", ".go", "main.go", ".go"),
        Create(Language.Rust, "Rust", ".rs", "main.rs", ".rs"),
        Create(Language.C, "C", ".c", "translated_code.c", ".c", ".h"),
        Create(Language.Cpp, "C++", ".cpp", "translated_code.cpp", ".cpp", ".cc", ".cxx", ".hpp", ".hh", ".hxx", ".h"),
        Create(Language.PHP, "PHP", ".php", "translated_code.php", ".php", ".phtml", ".php3", ".php4", ".php5", ".phps"),
        Create(Language.Kotlin, "Kotlin", ".kt", "Main.kt", ".kt", ".kts"),
        Create(Language.Swift, "Swift", ".swift", "main.swift", ".swift"),
        Create(Language.Ruby, "Ruby", ".rb", "translated_code.rb", ".rb", ".rake", ".gemspec")
    });

    public static IReadOnlyList<string> SourceExtensions { get; } = Array.AsReadOnly(
        All.SelectMany(item => item.SourceExtensions).Append(".txt").Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

    public static LanguageInfo Get(Language language) => All.FirstOrDefault(item => item.Language == language)
        ?? throw new ArgumentOutOfRangeException(nameof(language), language, "Choose a supported programming language.");

    private static LanguageInfo Create(Language language, string name, string extension, string fileName, params string[] extensions)
        => new(language, name, extension, fileName, Array.AsReadOnly(extensions));
}
