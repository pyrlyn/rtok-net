using System.Text.Json.Nodes;
using static Rtok.Measure.Tests.Lines;

namespace Rtok.Measure.Tests;

public sealed class DictionaryRowTests
{
    [Fact]
    public void Paths_of_16_bytes_and_identifiers_of_12_are_candidates()
    {
        var found = new List<(bool, string)>();

        DictionaryRow.Scan(
            "see src/widget/very_long_module_name.rs, the/path, compute_token_budget, "
                + "computeTokenBudget, short_id, deadbeefcafe0011 and https://example.com/a/b.",
            (isPath, s) => found.Add((isPath, s))
        );

        Assert.Equal(
            [
                (true, "src/widget/very_long_module_name.rs"),
                (false, "compute_token_budget"),
                (false, "computeTokenBudget"),
            ],
            found
        );
    }

    [Fact]
    public void Repeats_fill_the_row_and_no_repeat_leaves_it_empty()
    {
        const string Body =
            "src/widget/very_long_module_name.rs: compute_token_budget_for_request(&req)";

        Assert.False(Row(Body, Body, Body).IsEmpty);
        var once = Row(
            "src/widget/very_long_module_name.rs",
            "compute_token_budget_for_request",
            "short words only"
        );
        Assert.True(once.IsEmpty);
    }

    private static DictionaryRow Row(params string[] results)
    {
        var lines = new List<JsonNode>();
        for (var i = 0; i < results.Length; i++)
        {
            var use = Use($"t{i}", "Bash", Obj(("command", "true")));
            use["message"]!["usage"] = Obj(("input_tokens", 100 * (i + 1)));
            lines.Add(use);
            lines.Add(Result($"t{i}", results[i]));
        }
        var row = new DictionaryRow();
        row.Fold(Transcript.Parse(string.Concat(lines.Select(l => l.ToJsonString() + "\n"))));
        return row;
    }
}
