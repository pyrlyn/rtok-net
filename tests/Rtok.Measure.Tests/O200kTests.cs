using System.Text.Json;
using Microsoft.ML.Tokenizers;
using Rtok.Testing;

namespace Rtok.Measure.Tests;

/// <summary>
/// o200k counts for the fixture strings (<c>tests/fixtures/tokens/o200k.json</c>): a fixture's
/// token claim is checked against a real tokenizer, which the Release binary never carries.
/// </summary>
public sealed class O200kTests
{
    private static readonly Tokenizer O200k = TiktokenTokenizer.CreateForEncoding("o200k_base");

    public static TheoryData<string, int> Strings()
    {
        using var doc = JsonDocument.Parse(
            File.ReadAllText(RepoPaths.Fixture("tokens", "o200k.json"))
        );
        var data = new TheoryData<string, int>();
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            data.Add(e.GetProperty("text").GetString()!, e.GetProperty("tokens").GetInt32());
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Strings))]
    public void Counts_match_the_checked_in_counts(string text, int tokens) =>
        Assert.Equal(tokens, O200k.CountTokens(text));
}
