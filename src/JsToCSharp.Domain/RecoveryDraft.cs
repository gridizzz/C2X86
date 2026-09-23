namespace JsToCSharp.Domain;

public sealed record RecoveryDraft(string SourceCode, string ResultCode, Language SourceLanguage, Language TargetLanguage);
