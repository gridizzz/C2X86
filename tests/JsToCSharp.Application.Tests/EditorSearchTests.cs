using JsToCSharp.Application;
using Xunit;
namespace JsToCSharp.Application.Tests;

public sealed class EditorSearchTests
{
    [Theory]
    [InlineData("abc ABC", "abc", 3, true, 0)]
    [InlineData("abc ABC", "abc", 3, false, 4)]
    [InlineData("abc ABC", "missing", 0, false, -1)]
    [InlineData("abc", "", 0, false, -1)]
    [InlineData("abc", "abc", 3, true, 0)]
    [InlineData("a.b aXb", "a.b", 0, true, 0)]
    public void FindIsLiteralAndWraps(string text, string query, int start, bool matchCase, int expected)
        => Assert.Equal(expected, EditorSearch.FindNext(text, query, start, matchCase));

    [Theory]
    [InlineData("abc ABC", "abc", "$1", false, "$1 $1")]
    [InlineData("abc ABC", "abc", "", true, " ABC")]
    [InlineData("abc", "", "x", true, "abc")]
    [InlineData("aaa", "a", "aa", true, "aaaaaa")]
    public void ReplacementIsLiteralAndSupportsDeletion(string text, string query, string replacement, bool matchCase, string expected)
        => Assert.Equal(expected, EditorSearch.ReplaceAll(text, query, replacement, matchCase));
}
