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
        var rows = Rows();
        Assert.Equal(
            2 + AgentHosts.All.Sum(h => h.Variants.Count) + AgentHosts.Unsupported.Count,
            rows.Count
        );
        Assert.Contains(
            "| `goose` | Goose | Desktop | — | yes | — | — | mcp |",
            rows,
            StringComparer.Ordinal
        );
    }

    [Fact]
    public void Trae_has_a_row_and_the_reason_it_is_not_supported()
    {
        var table = SupportMatrix.Table();
        var trae = AgentHosts.Unsupported.Single(h =>
            string.Equals(h.Id, "trae", StringComparison.Ordinal)
        );

        Assert.Contains(
            "| `trae` | Trae | Desktop | — | — | — | — | — |",
            Rows(),
            StringComparer.Ordinal
        );
        Assert.Contains(
            $"- `trae`: {trae.Reason}. Checked 2026-10-22 against <https://docs.trae.ai/ide/add-mcp-servers>.\n",
            table,
            StringComparison.Ordinal
        );
    }

    private static List<string> Rows() =>
        [.. SupportMatrix.Table().Split('\n').Where(l => l.StartsWith('|'))];
}
