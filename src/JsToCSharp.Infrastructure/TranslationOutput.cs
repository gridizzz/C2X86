using System.Text.RegularExpressions;

namespace JsToCSharp.Infrastructure;

internal static partial class TranslationOutput
{
    // Strip only outer wrapper lines. Never replace backticks or language names inside code.
    public static string RemoveMarkdownFence(string output)
    {
        var candidate = output.Trim('\r', '\n');
        var opening = OpeningFence().Match(candidate);
        if (!opening.Success) return output;

        var code = candidate[opening.Length..];
        var lastNewline = code.LastIndexOf('\n');
        var lastLine = code[(lastNewline + 1)..];
        if (lastLine.Trim() == "```")
            code = lastNewline < 0 ? "" : code[..lastNewline].TrimEnd('\r');
        return code;
    }

    [GeneratedRegex(@"\A[ \t]*```[A-Za-z0-9_+#.\-]*[ \t]*\r?\n", RegexOptions.CultureInvariant)]
    private static partial Regex OpeningFence();
}
