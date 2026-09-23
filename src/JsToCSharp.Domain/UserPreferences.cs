namespace JsToCSharp.Domain;

// Explicit allowlist: credentials and source/output code never belong in preferences.
public sealed record UserPreferences
{
    public AiProvider Provider { get; init; } = AiProvider.OpenAi;
    public string? LocalModelPath { get; init; }
    public Language SourceLanguage { get; init; } = Language.JavaScript;
    public Language TargetLanguage { get; init; } = Language.CSharp;
    public double WindowWidth { get; init; } = 1180;
    public double WindowHeight { get; init; } = 760;
    public bool Maximized { get; init; }
    public double EditorFontSize { get; init; } = 14;
    public bool DraftRecoveryEnabled { get; init; }
    public bool WordWrap { get; init; }

    public UserPreferences Normalize() => this with
    {
        Provider = Enum.IsDefined(Provider) ? Provider : AiProvider.OpenAi,
        SourceLanguage = Enum.IsDefined(SourceLanguage) ? SourceLanguage : Language.JavaScript,
        TargetLanguage = Enum.IsDefined(TargetLanguage) ? TargetLanguage : Language.CSharp,
        WindowWidth = double.IsFinite(WindowWidth) ? Math.Clamp(WindowWidth, 900, 3840) : 1180,
        WindowHeight = double.IsFinite(WindowHeight) ? Math.Clamp(WindowHeight, 600, 2160) : 760,
        EditorFontSize = double.IsFinite(EditorFontSize) ? Math.Clamp(EditorFontSize, 10, 32) : 14,
        LocalModelPath = LocalModelPath?.Trim()
    };
}
