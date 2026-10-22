using System.Text.Json;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Graph.Tests;

// rtok graph/cochange.rs and the stale HEAD line of graph/mod.rs.
public sealed class CochangeTests
{
    // Three commits: a+b, a+b+c, and 23 files over the cap. Only the first two count.
    private static GraphHarness Scripted()
    {
        var h = new GraphHarness();
        TestGit.Init(h);
        TestGit.Commit(h, "one", "a.rs", "b.rs");
        TestGit.Commit(h, "two", "a.rs", "b.rs", "c.rs");
        TestGit.Commit(
            h,
            "three",
            [.. Enumerable.Range(0, 21).Select(i => $"w{i}.rs"), "a.rs", "b.rs"]
        );
        return h;
    }

    [Fact]
    public void Pairs_skip_a_commit_over_the_cap_and_a_pair_below_the_minimum()
    {
        using var h = Scripted();

        Assert.Equal([new CochangePair("a.rs", "b.rs", 2)], Cochange.Pairs(h.Cx, h.Root));
        Assert.Equal("changes with: b.rs (2)", Cochange.ChangesWith(h.Cx, h.Root, "a.rs"));
        Assert.Null(Cochange.ChangesWith(h.Cx, h.Root, "c.rs"));
    }

    // Seven partners changed with a.rs 7, 6, ... 1 times: the line names the top five by count.
    [Fact]
    public void Changes_with_names_at_most_five_partners_by_count()
    {
        using var h = new GraphHarness();
        TestGit.Init(h);
        for (var k = 0; k < 7; k++)
        {
            TestGit.Commit(
                h,
                $"c{k}",
                ["a.rs", .. Enumerable.Range(0, 7 - k).Select(i => $"p{i}.rs")]
            );
        }

        Assert.Equal(
            "changes with: p0.rs (7), p1.rs (6), p2.rs (5), p3.rs (4), p4.rs (3)",
            Cochange.ChangesWith(h.Cx, h.Root, "a.rs")
        );
    }

    [Fact]
    public void Pairs_recount_only_when_head_moves_and_skip_deleted_partners()
    {
        using var h = Scripted();
        var key = "cochange:" + h.Root;
        Assert.Null(h.Store.PluginStateGet("graph", key));
        Assert.Single(Cochange.Pairs(h.Cx, h.Root));
        var stored = h.Store.PluginStateGet("graph", key);
        Assert.NotNull(stored);
        var head = JsonSerializer.Deserialize(stored, GraphJson.Default.CochangeDoc)!.Head;
        // A document for the current HEAD is trusted as is.
        CochangePair[] fake = [new("x.rs", "y.rs", 9)];
        h.Store.PluginStateSet(
            "graph",
            key,
            JsonSerializer.Serialize(new CochangeDoc(head, fake), GraphJson.Default.CochangeDoc)
        );
        Assert.Equal(fake, Cochange.Pairs(h.Cx, h.Root));

        TestGit.Commit(h, "four", "a.rs", "b.rs");
        Assert.Equal([new CochangePair("a.rs", "b.rs", 3)], Cochange.Pairs(h.Cx, h.Root));
        File.Delete(h.Project.Combine("b.rs"));
        Assert.Null(Cochange.ChangesWith(h.Cx, h.Root, "a.rs"));
    }

    [Fact]
    public void Impact_lists_the_partners_of_the_defining_file()
    {
        using var h = new GraphHarness();
        TestGit.Init(h);
        foreach (var message in (string[])["one", "two"])
        {
            TestGit.Commit(h, message, "core.rs", "schema.sql", "docs/core.md");
        }

        TestGit.Commit(h, "three", "core.rs");
        h.Write("core.rs", "pub fn core_fn() {}\n");
        h.Write("caller.rs", "fn go() {\n    core_fn();\n}\n");

        var output = GraphTools.Impact(h.Cx, h.Project.Path, "core_fn");

        Assert.Contains("caller.rs", output, StringComparison.Ordinal);
        Assert.EndsWith(
            "changes with: docs/core.md (2), schema.sql (2)",
            output.TrimEnd(),
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "changes with",
            GraphTools.Impact(h.Cx, h.Project.Path, "go"),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void A_root_that_is_no_repository_has_no_pairs()
    {
        using var h = new GraphHarness();
        Assert.Empty(Cochange.Pairs(h.Cx, h.Root));
        Assert.Null(Cochange.ChangesWith(h.Cx, h.Root, "a.rs"));
    }

    [Fact]
    public void Count_dedupes_each_commit_and_drops_single_file_commits()
    {
        string[][] commits =
        [
            ["a.rs", "b.rs", "b.rs"],
            ["b.rs", "a.rs"],
            ["a.rs"],
            [],
        ];
        Assert.Equal([new CochangePair("a.rs", "b.rs", 2)], Cochange.Count(commits));
    }

    [Fact]
    public void A_moved_head_names_both_shas_when_the_index_is_not_rebuilt()
    {
        using var h = new GraphHarness();
        var plugin = new GraphPlugin();
        var args = JsonDocument.Parse("""{"name":"alpha"}""").RootElement;
        h.Write("a.rs", "fn alpha() {}\n");
        Assert.DoesNotContain(
            "index ",
            plugin.CallTool("symbol", args, h.Cx)!.Split('\n')[0],
            StringComparison.Ordinal
        );
        TestGit.Init(h);
        TestGit.Run(h.Project.Path, "add", "a.rs");
        TestGit.Run(h.Project.Path, "commit", "-q", "-m", "a");
        h.Write("a.rs", "fn alpha() {}\n");
        h.Run();
        var old = TestGit.Run(h.Project.Path, "rev-parse", "HEAD");
        TestGit.Run(h.Project.Path, "commit", "-q", "--allow-empty", "-m", "b");
        var head = TestGit.Run(h.Project.Path, "rev-parse", "HEAD");
        h.GraphJson = """{"auto_index":false}""";

        var output = plugin.CallTool("symbol", args, h.Cx)!;

        Assert.StartsWith($"index {old} head {head}\n", output, StringComparison.Ordinal);
        Assert.Contains("fn alpha()", output, StringComparison.Ordinal);
        var outline = plugin.CallTool(
            "outline",
            JsonDocument.Parse("""{"path":"a.rs"}""").RootElement,
            h.Cx
        )!;
        Assert.DoesNotContain("index ", outline, StringComparison.Ordinal);
    }
}
