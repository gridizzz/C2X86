namespace JsToCSharp.Domain;

public sealed record TranslationRequest(
    AiProvider Provider,
    string Model,
    string ApiKey,
    Language SourceLanguage,
    Language TargetLanguage,
    string SourceCode,
    string? LocalModelPath = null);
