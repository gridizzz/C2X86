using JsToCSharp.Domain;

namespace JsToCSharp.Application;

public sealed class TranslationService(IEnumerable<ICodeTranslator> translators)
{
    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken, IProgress<TranslationStage>? progress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Provider == AiProvider.LocalModel)
        {
            if (string.IsNullOrWhiteSpace(request.LocalModelPath))
                throw new ArgumentException("Browse for a local GGUF model file before translating.");
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.ApiKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Model);
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceCode);

        if (!Enum.IsDefined(request.SourceLanguage) || !Enum.IsDefined(request.TargetLanguage))
            throw new ArgumentException("Choose supported source and target languages.");

        if (request.SourceLanguage == request.TargetLanguage)
        {
            throw new ArgumentException("Choose two different languages.");
        }

        var translator = translators.FirstOrDefault(item => item.Provider == request.Provider)
            ?? throw new InvalidOperationException($"No translator is configured for {request.Provider}.");

        return await translator.TranslateAsync(request, cancellationToken, progress);
    }
}
