using JsToCSharp.Domain;

namespace JsToCSharp.Application;

public interface ICodeTranslator
{
    AiProvider Provider { get; }

    Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken, IProgress<TranslationStage>? progress = null);
}
