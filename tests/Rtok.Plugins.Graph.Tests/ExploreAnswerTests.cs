using System.Text;

namespace Rtok.Plugins.Graph.Tests;

// rtok graph/mod.rs, T68.1: the explore answer's sections over a fake backend.
public sealed class ExploreAnswerTests
{
    private sealed class FakeParts : IExploreParts
    {
        public IReadOnlyList<string> Resolve(string token) => token is "aa" or "bb" ? [token] : [];

        public string Defs(string name) => $"src/{name}.rs:1 function\nfn {name}() {{}}\n";

        public IReadOnlyList<string> Paths(string a, string b) =>
            a is "aa" && b is "bb" ? ["aa → cc → bb"] : [];

        public (string Text, int Rows) Impact1(string name) => ($"nothing reaches {name}", 0);

        public int DefCount(string name) => 1;
    }

    private static (string Text, long Before) Assemble(string query)
    {
        var parts = new FakeParts();
        return ExploreAnswer.Assemble(
            query,
            GraphFilter.None,
            parts,
            ExploreAnswer.Names(query, parts)
        );
    }

    [Fact]
    public void Sections_are_byte_exact_and_before_counts_the_replaced_calls()
    {
        var (text, before) = Assemble("aa bb zz");

        Assert.Equal(
            "= aa\nsrc/aa.rs:1 function\nfn aa() {}\n= bb\nsrc/bb.rs:1 function\nfn bb() {}\n"
                + "paths:\naa → cc → bb\nimpact:\naa ← 0\nbb ← 0\n",
            text
        );
        Assert.Equal(
            (
                (string[])
                    [
                        "src/aa.rs:1 function\nfn aa() {}\n",
                        "src/bb.rs:1 function\nfn bb() {}\n",
                        "nothing reaches aa",
                        "nothing reaches bb",
                    ]
            ).Sum(s => (long)Encoding.UTF8.GetByteCount(s)),
            before
        );
    }

    [Fact]
    public void No_name_and_no_path_have_their_own_lines()
    {
        Assert.Equal("no symbols resolved for \"zz\"", Assemble("zz").Text);
        var one = Assemble("aa").Text;
        Assert.Contains("paths:\nnone\n", one, StringComparison.Ordinal);
        Assert.Contains("impact:\naa ← 0\n", one, StringComparison.Ordinal);
    }

    [Fact]
    public void Tokens_split_the_query_and_drop_repeats()
    {
        Assert.Equal(
            ["how", "do", "Foo_bar", "and", "foo", "bar", "relate"],
            ExploreAnswer.Tokens("how do Foo_bar and foo-bar relate?")
        );
    }
}
