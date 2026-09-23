using System.Text;
using JsToCSharp.Application;
using JsToCSharp.Domain;
namespace JsToCSharp.Infrastructure;

public sealed class InputFileService : IInputFileService
{
    public async Task<string> ReadSourceAsync(Stream stream, string name)
    {
        if (!LanguageCatalog.SourceExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Choose a supported source file: {string.Join(", ", LanguageCatalog.SourceExtensions)}.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var buffer = new char[8192];
        var text = new StringBuilder();
        int count;
        while ((count = await reader.ReadAsync(buffer)) > 0)
        {
            if (text.Length + count > 2 * 1024 * 1024)
                throw new InvalidOperationException("Source file is too large. Import up to 2 million characters at a time.");
            if (buffer.AsSpan(0, count).Contains('\0'))
                throw new InvalidOperationException("This file contains binary data. Choose a text source file.");
            text.Append(buffer, 0, count);
        }
        return text.ToString();
    }

    public void ValidateModel(string path) => LocalCodeTranslator.ValidateModelFile(path);
}
