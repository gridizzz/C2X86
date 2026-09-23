namespace JsToCSharp.Application;

public static class EditorSearch
{
    public static int FindNext(string text, string query, int start, bool matchCase)
    {
        if (query.Length == 0) return -1;
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        start = Math.Clamp(start, 0, text.Length);
        var found = text.IndexOf(query, start, comparison);
        return found >= 0 ? found : text.IndexOf(query, 0, comparison);
    }

    public static string ReplaceAll(string text, string query, string replacement, bool matchCase) =>
        query.Length == 0 ? text : text.Replace(query, replacement,
            matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
}
