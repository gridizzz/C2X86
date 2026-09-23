using System.Text;
using JsToCSharp.Application;
using JsToCSharp.Domain;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace JsToCSharp.Infrastructure;

public sealed class LocalCodeTranslator : ICodeTranslator, ILocalModelRuntime
{
    private readonly ModelCache<LLamaWeights> _cache = new();
    public string? LoadedModelPath => _cache.LoadedKey;
    public Task UnloadAsync() => Task.Run(_cache.UnloadAsync);
    public ValueTask DisposeAsync() => new(Task.Run(async () => await _cache.DisposeAsync()));

    public AiProvider Provider => AiProvider.LocalModel;

    public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken, IProgress<TranslationStage>? progress = null)
        => Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateModelFile(request.LocalModelPath);
            try
            {
                var path = Path.GetFullPath(request.LocalModelPath!);
                var parameters = new ModelParams(path)
                {
                    ContextSize = 8192,
                    GpuLayerCount = 0
                };
                return await _cache.UseAsync(path, async token =>
                {
                    progress?.Report(TranslationStage.LoadingModel);
                    return await LLamaWeights.LoadFromFileAsync(parameters, token);
                }, async weights =>
                {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(TranslationStage.PreparingInput);
                var template = new LLamaTemplate(weights, strict: true) { AddAssistant = true };
                template.Add("user", TranslationPrompt.Create(request) + "\n/no_think");
                var prompt = Encoding.UTF8.GetString(template.Apply());
                const int outputBudget = 4096;
                if (weights.NativeHandle.Tokenize(prompt, true, true, Encoding.UTF8).Length + outputBudget > parameters.ContextSize)
                    throw new InvalidOperationException("Source is too long for the local context. Translate a smaller section of code.");

                var executor = new StatelessExecutor(weights, parameters) { ApplyTemplate = false };
                var output = new StringBuilder();
                await foreach (var text in executor.InferAsync(prompt, new InferenceParams
                {
                    MaxTokens = outputBudget,
                    SamplingPipeline = new DefaultSamplingPipeline { Temperature = 0.2f },
                    OverflowStrategy = ContextOverflowStrategy.ThrowException
                }, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (output.Length == 0) progress?.Report(TranslationStage.Generating);
                    output.Append(text);
                }
                cancellationToken.ThrowIfCancellationRequested();
                return new TranslationResult(CleanOutput(output.ToString()),
                    "Local translation complete. Review and test the result. Output is limited to 4096 tokens; check for incomplete code.");
                }, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) when (exception is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    "Local inference failed. Check that the GGUF model is supported, sufficient memory is available, and the CPU backend can load. " + exception.Message, exception);
            }
        }, cancellationToken);

    internal static void ValidateModelFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Browse for a local GGUF model file before translating.");
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[4];
        if (stream.Read(header) != 4 || !header.SequenceEqual("GGUF"u8))
            throw new InvalidOperationException("This is not a GGUF model. For Ollama, select the model weights blob, not its manifest or configuration.");
    }

    internal static string CleanOutput(string output)
    {
        var code = output;
        var leading = code.TrimStart();
        // Only a leading reasoning wrapper is metadata; a tag inside code is literal content.
        if (leading.StartsWith("<think>", StringComparison.Ordinal))
        {
            var endThinking = leading.IndexOf("</think>", StringComparison.Ordinal);
            if (endThinking < 0)
                throw new InvalidOperationException("The model exhausted its output while thinking. Try a smaller input or another model.");
            code = leading[(endThinking + "</think>".Length)..].TrimStart('\r', '\n');
        }
        else if (leading.StartsWith("</think>", StringComparison.Ordinal))
            code = leading["</think>".Length..].TrimStart('\r', '\n');
        code = TranslationOutput.RemoveMarkdownFence(code);
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("The local model returned no translated code. Try another model or a smaller input.");
        return code;
    }
}
