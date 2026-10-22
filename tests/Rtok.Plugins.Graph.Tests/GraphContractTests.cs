using System.Globalization;
using System.Text;
using System.Text.Json;
using Rtok.Plugin.Sdk;
using Rtok.Testing;

namespace Rtok.Plugins.Graph.Tests;

// rtok tests/graph_contract.rs: the five tools byte for byte through the plugin's tool call, the
// entry `rtok mcp` dispatches to (T15 adds the stdio process around it), plus what a caller sees
// of the index: a second repo leaves the first intact, an edit is re-read, a deletion drops rows.
// The nameless `callers` (dead definitions) and the path-only `impact` (affected tests) are T25.
public sealed class GraphContractTests
{
    private const string B =
        "chain.rs::b#function@4 chain.rs:4 function\nfn b() {\n    c();\n}\ncalls: c\n";
    private const string C = "chain.rs::c#function@7 chain.rs:7 function\nfn c() {}\n";

    private static GraphHarness Repo()
    {
        var h = new GraphHarness();
        h.Write("chain.rs", GraphToolsTests.Chain);
        h.Write("other.rs", GraphToolsTests.Other);
        return h;
    }

    private static string? Call(Ctx cx, string tool, string args)
    {
        using var doc = JsonDocument.Parse(args);
        return new GraphPlugin().CallTool(tool, doc.RootElement, cx);
    }

    [Fact]
    public void Explore_answers_a_two_symbol_question_byte_exact()
    {
        using var h = Repo();
        Assert.Equal(
            "= b\n" + B + "= c\n" + C + "paths:\nc → b\nimpact:\nb ← 1\nc ← 2\n",
            Call(h.Cx, "explore", """{"query":"how do b and c interact"}""")
        );
        Assert.Equal(
            "no symbols resolved for \"zzz nothing\"",
            Call(h.Cx, "explore", """{"query":"zzz nothing"}""")
        );
        Assert.Equal(
            "= b\nno definition of b in other\n= c\nno definition of c in other\n"
                + "paths:\nc → b\nimpact:\nb ← 0\nc ← 1\n",
            Call(h.Cx, "explore", """{"query":"b and c","path":"other"}""")
        );
    }

    [Fact]
    public void Four_tools_answer_byte_exact()
    {
        using var h = Repo();
        Assert.Equal(B, Call(h.Cx, "symbol", """{"name":"b"}"""));
        Assert.Equal(
            "chain.rs  b ×1 (L5)\nother.rs  d ×2 (L2)\n",
            Call(h.Cx, "callers", """{"name":"c"}""")
        );
        Assert.Equal(
            "1  chain.rs  b\n1  other.rs  d\n2  chain.rs  a\n",
            Call(h.Cx, "impact", """{"name":"c","depth":2}""")
        );
        Assert.Equal(
            "1  chain.rs  b\n1  other.rs  d\n",
            Call(h.Cx, "impact", """{"name":"c","depth":1}""")
        );
        Assert.Equal("fn a 1\nfn b 4\nfn c 7", Call(h.Cx, "outline", """{"path":"chain.rs"}"""));
        Assert.Equal("no definition of zzz", Call(h.Cx, "symbol", """{"name":"zzz"}"""));
        Assert.Equal("no references to a", Call(h.Cx, "callers", """{"name":"a"}"""));
        Assert.Equal("nothing reaches a", Call(h.Cx, "impact", """{"name":"a"}"""));
        Assert.Equal("= b\n" + B + "= c\n" + C, Call(h.Cx, "symbol", """{"names":["b","c"]}"""));
        Assert.Equal(
            "= b\n" + B + "= zzz\nno definition of zzz\n",
            Call(h.Cx, "symbol", """{"names":["b","zzz"]}""")
        );
    }

    [Fact]
    public void A_second_repo_leaves_the_first_intact()
    {
        using var h = Repo();
        using var other = new TempDir("rtok-graph-b-");
        other.Write("chain.rs", "fn alpha() {}\n");
        var b = new Ctx(new StoreHost(h.Store) { Cwd = other.Path });
        var first = Call(h.Cx, "symbol", """{"name":"a"}""");

        Assert.Equal(
            "chain.rs::a#function@1 chain.rs:1 function\nfn a() {\n    b();\n}\ncalls: b\n",
            first
        );
        Assert.Equal("no definition of a", Call(b, "symbol", """{"name":"a"}"""));
        Assert.Equal(first, Call(h.Cx, "symbol", """{"name":"a"}"""));
    }

    [Fact]
    public void Edited_and_deleted_files_are_reflected()
    {
        using var h = Repo();
        Assert.Equal(
            "chain.rs  b ×1 (L5)\nother.rs  d ×2 (L2)\n",
            Call(h.Cx, "callers", """{"name":"c"}""")
        );
        h.Write("other.rs", "fn d() {\n    c();\n    c();\n    c();\n}\n");
        Assert.Equal(
            "chain.rs  b ×1 (L5)\nother.rs  d ×3 (L2)\n",
            Call(h.Cx, "callers", """{"name":"c"}""")
        );
        File.Delete(h.Project.Combine("other.rs"));
        Assert.Equal("chain.rs  b ×1 (L5)\n", Call(h.Cx, "callers", """{"name":"c"}"""));
    }

    [Fact]
    public void Filters_narrow_to_one_subtree()
    {
        using var h = Repo();
        Assert.Equal(B, Call(h.Cx, "symbol", """{"name":"b","path":"chain"}"""));
        Assert.Equal(
            "no definition of b of kind struct",
            Call(h.Cx, "symbol", """{"name":"b","kind":"struct"}""")
        );
        Assert.Equal(
            "other.rs  d ×2 (L2)\n",
            Call(h.Cx, "callers", """{"name":"c","path":"other"}""")
        );
        Assert.Equal(
            "1  other.rs  d\n",
            Call(h.Cx, "impact", """{"name":"c","depth":2,"path":"other"}""")
        );
    }

    [Fact]
    public void A_long_body_expands_and_an_id_selects_one_row()
    {
        using var h = new GraphHarness();
        var src = new StringBuilder("fn long() {\n");
        for (var i = 0; i < 50; i++)
        {
            src.Append(CultureInfo.InvariantCulture, $"    let v{i} = {i};\n");
        }

        h.Write("long.rs", src.Append("}\nfn short() {}\n").ToString());
        var cx = h.Cx;
        var output = Call(cx, "symbol", """{"name":"long"}""")!;

        Assert.Contains("long.rs::long#function@1", output, StringComparison.Ordinal);
        Assert.DoesNotContain("let v49", output, StringComparison.Ordinal);
        var shown = output
            .Split('\n')
            .Select(l => l.Split("expand ", 2))
            .First(p => p.Length == 2)[1]
            .Trim();
        var full = Encoding.UTF8.GetString(h.Host.Expand(shown));
        Assert.Contains("fn long()", full, StringComparison.Ordinal);
        Assert.Contains("let v49 = 49;", full, StringComparison.Ordinal);
        var byId = Call(cx, "symbol", """{"id":"long.rs::short#function@53","name":"long"}""")!;
        Assert.Contains("fn short()", byId, StringComparison.Ordinal);
        Assert.DoesNotContain("fn long()", byId, StringComparison.Ordinal);
        Assert.Equal(
            "no definition of long.rs::missing#function@1",
            Call(cx, "symbol", """{"id":"long.rs::missing#function@1"}""")
        );
    }

    [Fact]
    public void Outline_takes_a_kind()
    {
        using var h = Repo();
        h.Write("mix.rs", "struct S;\nfn x() {}\nenum E {\n    A,\n}\n");
        Assert.Equal("struct S 1", Call(h.Cx, "outline", """{"path":"mix.rs","kind":"struct"}"""));
        Assert.Equal(
            "no trait in mix.rs",
            Call(h.Cx, "outline", """{"path":"mix.rs","kind":"trait"}""")
        );
        Assert.Equal(
            "struct S 1\nfn x 2\nenum E 3",
            Call(h.Cx, "outline", """{"path":"mix.rs"}""")
        );
    }
}
