using System.Text;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Graph.Tests;

// rtok graph/mod.rs: symbol, callers, impact, outline and explore over the tags index.
public sealed class GraphToolsTests
{
    internal const string Chain = "fn a() {\n    b();\n}\nfn b() {\n    c();\n}\nfn c() {}\n";
    internal const string Other = "fn d() {\n    c();\n    c();\n}\n";

    private static string Root(GraphHarness h) => h.Project.Path;

    private static string LastLine(string text) => text.TrimEnd('\n').Split('\n')[^1];

    private static string Shown(string trailer) => trailer[(trailer.LastIndexOf(' ') + 1)..];

    private static int Lines(string text, Func<string, bool> keep) => text.Split('\n').Count(keep);

    [Fact]
    public void Symbol_returns_the_body_and_its_callees_and_records_no_row_under_the_cap()
    {
        using var h = new GraphHarness();
        h.Write("chain.rs", Chain);

        Assert.Equal(
            "chain.rs::a#function@1 chain.rs:1 function\nfn a() {\n    b();\n}\ncalls: b\n",
            GraphTools.Symbol(h.Cx, Root(h), "a")
        );
        Assert.Contains(
            "calls: c",
            GraphTools.Symbol(h.Cx, Root(h), "b"),
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "calls:",
            GraphTools.Symbol(h.Cx, Root(h), "c"),
            StringComparison.Ordinal
        );
        Assert.Equal("no definition of no_such_fn", GraphTools.Symbol(h.Cx, Root(h), "no_such_fn"));
        Assert.Empty(h.Host.Memory.Recorded());
    }

    [Fact]
    public void Five_hundred_definitions_are_capped_and_the_archive_holds_them_all()
    {
        using var h = new GraphHarness();
        h.Write("d.rs", string.Concat(Enumerable.Repeat("fn dup() {\n    ();\n}\n", 500)));

        var output = GraphTools.Symbol(h.Cx, Root(h), "dup");

        var trailer = LastLine(output);
        Assert.Contains(" more, expand ", trailer, StringComparison.Ordinal);
        var full = Encoding.UTF8.GetString(h.Host.Expand(Shown(trailer)));
        Assert.Equal(500, Lines(full, l => l.StartsWith("fn dup()", StringComparison.Ordinal)));
        Assert.True(h.Host.Estimate(output, TextClass.Code) <= 2000);
        Assert.Single(h.Host.Memory.Recorded());
    }

    [Fact]
    public void A_cjk_body_is_capped_in_tokens_not_characters()
    {
        using var h = new GraphHarness();
        var body = string.Concat(
            Enumerable.Repeat("// 漢字漢字漢字漢字漢字漢字漢字漢字漢字漢字\n", 3000)
        );
        h.Write("cjk.rs", "fn cjk() {\n" + body + "}");

        var output = GraphTools.Symbol(h.Cx, Root(h), "cjk");

        Assert.True(h.Host.Estimate(output, TextClass.Code) <= 2000, output);
    }

    [Fact]
    public void Callers_names_the_calling_definition_and_its_count()
    {
        using var h = new GraphHarness();
        h.Write("chain.rs", Chain);
        h.Write("other.rs", Other);

        var output = GraphTools.Callers(h.Cx, Root(h), "c");

        Assert.Contains(
            output.Split('\n'),
            l => l.StartsWith("other.rs  d ×2", StringComparison.Ordinal)
        );
        Assert.Contains(
            output.Split('\n'),
            l => l.StartsWith("chain.rs  b ×1", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Five_hundred_callers_are_capped_with_one_row_that_keeps_the_full_id()
    {
        using var h = new GraphHarness();
        var src = new StringBuilder("fn zeta() {}\n");
        for (var i = 0; i < 500; i++)
        {
            src.Append("fn c").Append(i).Append("() { zeta(); }\n");
        }

        h.Write("zeta.rs", src.ToString());

        var output = GraphTools.Callers(h.Cx, Root(h), "zeta");

        var shown = Shown(LastLine(output));
        var full = Encoding.UTF8.GetString(h.Host.Expand(shown));
        Assert.Equal(500, full.TrimEnd('\n').Split('\n').Length);
        Assert.True(h.Host.Estimate(output, TextClass.Code) <= 2000);
        var row = Assert.Single(h.Host.Memory.Recorded());
        Assert.Equal("cap", row.Kind);
        Assert.Equal(Encoding.UTF8.GetByteCount(output), row.AfterBytes);
        Assert.True(row.AfterBytes < row.BeforeBytes);
        Assert.True(row.EstAfter < row.EstBefore);
        Assert.StartsWith(shown, row.RefId, StringComparison.Ordinal);
        Assert.True(row.RefId!.Length > shown.Length);
    }

    [Fact]
    public void Impact_walks_the_call_chain_to_its_depth_and_terminates_on_a_cycle()
    {
        using var h = new GraphHarness();
        h.Write(
            "chain.rs",
            "fn a() {\n    b();\n}\nfn b() {\n    c();\n}\nfn x() {\n    y();\n    c();\n}\nfn y() {\n    x();\n}\n"
        );
        string? At(string output, string name) =>
            output
                .Split('\n')
                .FirstOrDefault(l => l.EndsWith("  " + name, StringComparison.Ordinal))
                ?[..1];

        var two = GraphTools.Impact(h.Cx, Root(h), "c", 2);
        Assert.Equal("1", At(two, "b"));
        Assert.Equal("2", At(two, "a"));
        var one = GraphTools.Impact(h.Cx, Root(h), "c", 1);
        Assert.Equal("1", At(one, "b"));
        Assert.Null(At(one, "a"));
        var deep = GraphTools.Impact(h.Cx, Root(h), "c", 4);
        Assert.Equal(1, Lines(deep, l => l.EndsWith("  x", StringComparison.Ordinal)));
        Assert.Equal("nothing reaches no_such_fn", GraphTools.Impact(h.Cx, Root(h), "no_such_fn"));
    }

    [Fact]
    public void Impact_to_prints_the_chains_between_two_symbols()
    {
        using var h = new GraphHarness();
        h.Write("chain.rs", Chain);

        Assert.Equal("a → b → c\n", GraphTools.Impact(h.Cx, Root(h), "a", 3, to: "c"));
        Assert.Equal("a → b\n", GraphTools.Impact(h.Cx, Root(h), "a", 3, to: "b"));
        Assert.Equal(
            "no path from b to a within depth 3",
            GraphTools.Impact(h.Cx, Root(h), "b", 3, to: "a")
        );
        Assert.Equal(
            "no path from a to c within depth 1",
            GraphTools.Impact(h.Cx, Root(h), "a", 1, to: "c")
        );
    }

    [Fact]
    public void Callers_and_impact_mark_ambiguous_names()
    {
        using var h = new GraphHarness();
        h.Write("a.rs", "fn new() {}\nfn alpha() { new(); }\n");
        h.Write("b.rs", "fn new() {}\n");

        Assert.DoesNotContain('?', GraphTools.Callers(h.Cx, Root(h), "alpha"));
        var callers = GraphTools.Callers(h.Cx, Root(h), "new");
        Assert.StartsWith("1 names ambiguous", callers, StringComparison.Ordinal);
        Assert.Contains(" ?\n", callers, StringComparison.Ordinal);
        Assert.StartsWith(
            "1 names ambiguous",
            GraphTools.Impact(h.Cx, Root(h), "new", 1),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void An_import_picks_one_of_two_same_named_definitions()
    {
        using var h = new GraphHarness();
        h.Write("src/a.rs", "pub fn parse() {}\n");
        h.Write("src/b.rs", "pub fn parse() {}\nfn local() { parse(); }\n");
        h.Write("src/c.rs", "use crate::a::parse;\nfn go() { parse(); }\n");
        // Two references in three files are most of the index: pad it past the 5 % cutoff.
        for (var i = 0; i < 40; i++)
        {
            h.Write($"src/p{i}.rs", $"fn pad{i}() {{}}\n");
        }

        foreach (
            var output in (string[])
                [
                    GraphTools.Callers(h.Cx, Root(h), "parse"),
                    GraphTools.Impact(h.Cx, Root(h), "parse", 1),
                ]
        )
        {
            Assert.Contains("src/c.rs", output, StringComparison.Ordinal);
            Assert.DoesNotContain(
                output.Split('\n'),
                l => l.Contains("src/b.rs", StringComparison.Ordinal)
            );
            Assert.Contains("+1 other definitions of parse", output, StringComparison.Ordinal);
        }

        var explored = GraphTools.Explore(h.Cx, Root(h), "parse");
        Assert.Contains("+1 other definitions of parse", explored, StringComparison.Ordinal);
        Assert.Contains("src/a.rs", explored, StringComparison.Ordinal);
        Assert.DoesNotContain("src/b.rs:", explored, StringComparison.Ordinal);
    }

    [Fact]
    public void Path_and_kind_filters_narrow_every_tool_and_name_the_scope_when_empty()
    {
        using var h = new GraphHarness();
        h.Write("a.rs", "fn dup() {}\n");
        h.Write("b.rs", "fn dup() {}\n");
        h.Write("k.rs", "struct Shape;\nfn Shape() {}\n");
        h.Write("chain.rs", Chain);
        h.Write("other.rs", Other);

        var both = GraphTools.Symbol(h.Cx, Root(h), "dup");
        Assert.Contains("a.rs", both, StringComparison.Ordinal);
        Assert.Contains("b.rs", both, StringComparison.Ordinal);
        var one = GraphTools.Symbol(h.Cx, Root(h), "dup", new GraphFilter("b.rs"));
        Assert.Contains("b.rs", one, StringComparison.Ordinal);
        Assert.DoesNotContain("a.rs", one, StringComparison.Ordinal);
        Assert.Equal(
            "no definition of dup in zzz",
            GraphTools.Symbol(h.Cx, Root(h), "dup", new GraphFilter("zzz"))
        );
        // A struct is a `class` in the tags grammar; the asked kind keeps it over the function.
        var shape = GraphTools.Symbol(h.Cx, Root(h), "Shape", new GraphFilter(Kind: "class"));
        Assert.Contains("struct Shape;", shape, StringComparison.Ordinal);
        Assert.DoesNotContain("fn Shape", shape, StringComparison.Ordinal);
        Assert.Equal(
            "no definition of Shape of kind struct",
            GraphTools.Symbol(h.Cx, Root(h), "Shape", new GraphFilter(Kind: "struct"))
        );
        var callers = GraphTools.Callers(h.Cx, Root(h), "c", new GraphFilter("other"));
        Assert.Contains("other.rs", callers, StringComparison.Ordinal);
        Assert.DoesNotContain("chain.rs", callers, StringComparison.Ordinal);
        Assert.Equal(
            "1  other.rs  d\n",
            GraphTools.Impact(h.Cx, Root(h), "c", 2, new GraphFilter("other"))
        );
        Assert.Equal(
            "nothing reaches c in zzz",
            GraphTools.Impact(h.Cx, Root(h), "c", 2, new GraphFilter("zzz"))
        );
    }

    [Fact]
    public void Without_auto_index_an_edit_is_stale_until_the_next_index()
    {
        using var h = new GraphHarness("""{"auto_index":false}""");
        var file = h.Write("a.rs", "fn alpha() {}\n");
        Assert.Contains(
            "a.rs:1",
            GraphTools.Symbol(h.Cx, Root(h), "alpha"),
            StringComparison.Ordinal
        );
        File.WriteAllText(file, "// bump\nfn alpha() {}\n");
        File.SetLastWriteTimeUtc(file, File.GetLastWriteTimeUtc(file).AddSeconds(5));

        var stale = GraphTools.Symbol(h.Cx, Root(h), "alpha");

        Assert.StartsWith("stale: 1 files pending (a.rs)\n", stale, StringComparison.Ordinal);
        Assert.Contains("a.rs:1", stale, StringComparison.Ordinal);
        Assert.Equal(0, Indexer.Ensure(h.Cx, Root(h)).Read);
        h.Run();
        Assert.Contains(
            "a.rs:2",
            GraphTools.Symbol(h.Cx, Root(h), "alpha"),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Without_auto_index_an_empty_root_is_still_indexed_once()
    {
        using var h = new GraphHarness("""{"auto_index":false}""");
        h.Write("main.rs", "fn main() {}\n");
        Assert.Contains(
            "main.rs:1",
            GraphTools.Symbol(h.Cx, Root(h), "main"),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void An_id_selects_one_definition()
    {
        using var h = new GraphHarness();
        h.Write("a.rs", "fn dup() {}\n");
        h.Write("b.rs", "fn dup() { let x = 1; }\n");

        var output = GraphTools.SymbolById(h.Cx, Root(h), "b.rs::dup#function@1");

        Assert.Contains("b.rs::dup#function@1 b.rs:1 function", output, StringComparison.Ordinal);
        Assert.Contains("let x = 1", output, StringComparison.Ordinal);
        Assert.DoesNotContain("a.rs", output, StringComparison.Ordinal);
        Assert.Equal("no definition of nope", GraphTools.SymbolById(h.Cx, Root(h), "nope"));
    }

    [Fact]
    public void A_long_body_archives_the_uncut_span()
    {
        using var h = new GraphHarness();
        var src = new StringBuilder("fn long() {\n");
        for (var i = 0; i < 50; i++)
        {
            src.Append("    let v").Append(i).Append(" = ").Append(i).Append(";\n");
        }

        var file = h.Write("long.rs", src.Append("}\n").ToString());

        var output = GraphTools.Symbol(h.Cx, Root(h), "long");

        Assert.DoesNotContain("let v49", output, StringComparison.Ordinal);
        var shown = output
            .Split('\n')
            .Select(l => l.Split("expand "))
            .First(p => p.Length > 1)[1]
            .Trim();
        var span = h.Store.GetSymbolSpan(h.Root, "long.rs", "long", "function", 1);
        Assert.NotNull(span);
        var bytes = File.ReadAllBytes(file);
        Assert.Equal(bytes[(int)span.StartByte..(int)span.EndByte], h.Host.Expand(shown));
    }

    [Fact]
    public void A_changed_file_prints_stale_and_is_not_sliced()
    {
        using var h = new GraphHarness();
        var file = h.Write("a.rs", "fn alpha() { let n = 1; }\n");
        Assert.Contains(
            "fn alpha()",
            GraphTools.Symbol(h.Cx, Root(h), "alpha"),
            StringComparison.Ordinal
        );
        h.GraphJson = """{"auto_index":false}""";
        File.WriteAllText(file, "/* shifted */\nfn alpha() { let n = 1; }\n");

        var output = GraphTools.Symbol(h.Cx, Root(h), "alpha");

        Assert.Contains("stale a.rs\n", output, StringComparison.Ordinal);
        Assert.DoesNotContain("shifted", output, StringComparison.Ordinal);
        Assert.Contains("a.rs:1 function", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Explore_answers_a_two_symbol_question_byte_exact()
    {
        using var h = new GraphHarness();
        h.Write("chain.rs", Chain);
        h.Write("other.rs", Other);

        Assert.Equal(
            "= b\nchain.rs::b#function@4 chain.rs:4 function\nfn b() {\n    c();\n}\ncalls: c\n"
                + "= c\nchain.rs::c#function@7 chain.rs:7 function\nfn c() {}\n"
                + "paths:\nc → b\n"
                + "impact:\nb ← 1\nc ← 2\n",
            GraphTools.Explore(h.Cx, Root(h), "how do b and c interact")
        );
    }

    [Fact]
    public void Explore_resolves_a_prefix_by_reference_count_and_keeps_a_path()
    {
        using var h = new GraphHarness();
        h.Write(
            "p.rs",
            "fn alpha() {}\nfn alphabet() {}\nfn user() {\n    alphabet();\n    alpha();\n    alphabet();\n}\n"
        );
        h.Write("a.rs", "fn dup() {}\n");
        h.Write("b.rs", "fn dup() {}\n");

        var output = GraphTools.Explore(h.Cx, Root(h), "alph");
        Assert.True(
            output.IndexOf("= alphabet\n", StringComparison.Ordinal)
                < output.IndexOf("= alpha\n", StringComparison.Ordinal),
            output
        );
        Assert.Contains("= alpha\n", output, StringComparison.Ordinal);
        Assert.Equal(
            "no symbols resolved for \"zz zz\"",
            GraphTools.Explore(h.Cx, Root(h), "zz zz")
        );
        var one = GraphTools.Explore(h.Cx, Root(h), "dup", new GraphFilter("b.rs"));
        Assert.Contains("b.rs:1", one, StringComparison.Ordinal);
        Assert.DoesNotContain("a.rs:1", one, StringComparison.Ordinal);
    }

    [Fact]
    public void Explore_falls_back_to_the_names_and_docs_a_query_matches()
    {
        using var h = new GraphHarness();
        h.Write(
            "body.rs",
            "/// Shows the truncated source lines of a definition.\nfn body_lines() {}\n"
        );
        Assert.Contains(
            "body_lines",
            GraphTools.Explore(h.Cx, Root(h), "truncated source lines"),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Outline_is_reads_map_narrowed_to_a_kind_and_stays_inside_the_root()
    {
        using var h = new GraphHarness();
        h.Write("src/main.rs", "struct Point;\nfn main() {}\nfn helper() {}\n");

        var map = GraphTools.Outline(h.Cx, Root(h), "src/main.rs");
        Assert.Contains("main", map, StringComparison.Ordinal);
        Assert.Contains("Point", map, StringComparison.Ordinal);
        var fns = GraphTools.Outline(h.Cx, Root(h), "src/main.rs", "fn");
        Assert.DoesNotContain("Point", fns, StringComparison.Ordinal);
        Assert.Contains("helper", fns, StringComparison.Ordinal);
        Assert.Equal(
            "no enum in src/main.rs",
            GraphTools.Outline(h.Cx, Root(h), "src/main.rs", "enum")
        );
        Assert.Throws<ArgumentException>(() => GraphTools.Outline(h.Cx, Root(h), "../x.rs"));
        Assert.Throws<FileNotFoundException>(() => GraphTools.Outline(h.Cx, Root(h), "gone.rs"));
    }
}
