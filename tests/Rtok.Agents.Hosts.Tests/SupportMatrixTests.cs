using Rtok.Testing;

namespace Rtok.Agents.Hosts.Tests;

public sealed class SupportMatrixTests
{
    /// <summary>
    /// The table in <c>docs/agents.md</c> is the host table's: run with <c>RTOK_BLESS=1</c> to
    /// rewrite it after changing a host.
    /// </summary>
    [Fact]
    public void The_doc_table_matches_the_host_table()
    {
        var path = Path.Combine(RepoPaths.Root, "docs", "agents.md");
        var doc = File.ReadAllText(path);
        var start = doc.IndexOf(SupportMatrix.Start, StringComparison.Ordinal);
        var end = doc.IndexOf(SupportMatrix.End, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"{path} has no agents-table markers");
        var old = doc[(start + SupportMatrix.Start.Length)..end];
        var table = SupportMatrix.Table();
        if (string.Equals(old, table, StringComparison.Ordinal))
        {
            return;
        }
        if (Environment.GetEnvironmentVariable("RTOK_BLESS") is not null)
        {
            File.WriteAllText(
                path,
                doc[..(start + SupportMatrix.Start.Length)] + table + doc[end..]
            );
            return;
        }
        Assert.Fail(
            "docs/agents.md host table is stale; run `RTOK_BLESS=1 dotnet test --project tests/Rtok.Agents.Hosts.Tests`\n"
                + $"--- doc\n{old}--- code\n{table}"
        );
    }

    [Fact]
    public void Every_app_has_a_row()
    {
        var rows = SupportMatrix.Table().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2 + AgentHosts.All.Sum(h => h.Variants.Count), rows.Length);
        Assert.Contains(
            "| `goose` | Goose | Desktop | — | yes | — | — | mcp |",
            rows,
            StringComparer.Ordinal
        );
    }
}
