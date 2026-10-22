using System.Globalization;
using System.Text;

namespace Rtok.Plugins.Cmd.Tests;

/// <summary>rtok's <c>grep.rs</c> tests: grep / rg / git grep hit lists.</summary>
public sealed class GrepTests
{
    private static string[] Argv(string s) => Text.Words(s);

    [Fact]
    public void Prints_each_path_once_and_keeps_every_path() =>
        Assert.Equal(
            "src/a.rs\n10:let x = hit;\n20:let y = hit;\nsrc/b.rs:3:fn hit() {}",
            Grep.Group(
                Argv("grep -rn hit src"),
                "src/a.rs:10:    let x = hit;\nsrc/a.rs:20:    let y = hit;\nsrc/b.rs:3:fn hit() {}\n"
            )
        );

    [Fact]
    public void Files_are_path_sorted()
    {
        var got = Grep.Group(Argv("rg -n a"), "z.rs:1:a\nz.rs:2:a\na.rs:1:a\na.rs:2:a\n");
        Assert.NotNull(got);
        Assert.True(
            got.IndexOf("a.rs", StringComparison.Ordinal)
                < got.IndexOf("z.rs", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Per_file_budget_ends_in_a_count()
    {
        var raw = string.Concat(Enumerable.Range(1, 30).Select(n => $"big.rs:{n}:hit {n}\n"));
        var got = Grep.Group(Argv("grep -rn hit ."), raw);
        Assert.NotNull(got);
        Assert.Contains("… +18 more", got, StringComparison.Ordinal);
        Assert.Contains("12:hit 12", got, StringComparison.Ordinal);
        Assert.DoesNotContain("13:hit 13", got, StringComparison.Ordinal);
    }

    [Fact]
    public void Files_past_the_budget_keep_path_and_line_numbers()
    {
        var raw = string.Concat(
            Enumerable
                .Range(1, 40)
                .Select(n =>
                    $"f{n:D2}.rs:{n}:fn main() {{}}\nf{n:D2}.rs:{n + 100}:fn main() {{}}\n"
                )
        );
        var got = Grep.Group(Argv("grep -rn main ."), raw);
        Assert.NotNull(got);
        for (var n = 1; n <= 40; n++)
        {
            Assert.Contains($"f{n:D2}.rs", got, StringComparison.Ordinal);
        }

        Assert.Contains("f40.rs:40,140", got, StringComparison.Ordinal);
        Assert.True(got.Split('\n').Length < 80);
    }

    [Fact]
    public void Long_line_is_cut_around_the_match()
    {
        var pad = new string('x', 300);
        var got = Grep.Group(
            Argv("grep -rn NEEDLE ."),
            $"a.rs:1:{pad}NEEDLE{pad}\na.rs:2:NEEDLE\n"
        );
        Assert.NotNull(got);
        var first = got.Split('\n')[1];
        Assert.Contains("NEEDLE", first, StringComparison.Ordinal);
        Assert.True(Text.Bytes(first) < 160, first);
        Assert.StartsWith("1:…", first, StringComparison.Ordinal);
        Assert.EndsWith("…", first, StringComparison.Ordinal);
    }

    [Fact]
    public void Case_insensitive_pattern_still_finds_the_window()
    {
        var pad = new string('x', 200);
        var got = Grep.Group(
            Argv("grep -rni needle ."),
            $"a.rs:1:{pad}Needle{pad}\na.rs:2:needle\n"
        );
        Assert.NotNull(got);
        Assert.Contains("Needle", got.Split('\n')[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Other_shapes_fall_back()
    {
        Assert.Null(Grep.Group(Argv("grep -rc hit src"), "a.rs:3\nb.rs:4\n"));
        Assert.Null(Grep.Group(Argv("rg -l hit"), "a.rs\nb.rs\n"));
        Assert.Null(Grep.Group(Argv("grep -rn -A2 hit ."), "a.rs:1:hit\na.rs-2-x\n"));
        Assert.Null(Grep.Group(Argv("grep -n hit f"), "3:hit\n"));
    }

    [Fact]
    public void Stderr_and_binary_lines_survive()
    {
        var got = Grep.Group(
            Argv("grep -rn hit ."),
            "a.rs:1:hit\na.rs:2:hit\nBinary file x.bin matches\ngrep: gone: No such file or directory\n"
        );
        Assert.NotNull(got);
        Assert.Contains("Binary file x.bin matches", got, StringComparison.Ordinal);
        Assert.Contains("grep: gone: No such file or directory", got, StringComparison.Ordinal);
    }

    [Fact]
    public void Never_longer_than_raw() =>
        Assert.Null(Grep.Group(Argv("grep -rn hit ."), "a.rs:1:hit\n"));

    [Fact]
    public void Git_grep_and_unnumbered_output() =>
        Assert.Equal(
            "src/a.rs\nlet hit = 1;\nlet hit = 2;",
            Grep.Group(Argv("git grep hit"), "src/a.rs:let hit = 1;\nsrc/a.rs:let hit = 2;\n")
        );

    // A pattern this engine cannot read cuts no window, and the grouping still applies.
    [Fact]
    public void An_unreadable_pattern_keeps_whole_lines()
    {
        var pad = new string('x', 200);
        var got = Grep.Group(
            Argv("grep -rnE (unclosed ."),
            $"a.rs:1:{pad}(unclosed\na.rs:2:(unclosed\n"
        );
        Assert.NotNull(got);
        Assert.Contains($"1:{pad}(unclosed", got, StringComparison.Ordinal);
    }

    // grep, rg and git grep print each path once with ±60 chars around a match, 12 matches
    // per file and a 16-line budget, then `path:l1,l2` anchors.
    [Theory]
    [InlineData("grep -rn parse_config src")]
    [InlineData("rg -n parse_config")]
    [InlineData("git grep -n parse_config")]
    public Task Grep_rg_and_git_grep_share_one_shape(string cmd)
    {
        var raw = new StringBuilder();
        var pad = new string('y', 100);
        for (var n = 1; n <= 14; n++)
        {
            raw.Append(
                CultureInfo.InvariantCulture,
                $"src/loader.rs:{n * 10}:    let cfg = parse_config(&raw_{n});\n"
            );
        }

        raw.Append(
            CultureInfo.InvariantCulture,
            $"src/mod.rs:57:{pad} parse_config(&text) {pad}\n"
        );
        for (var f = 0; f < 4; f++)
        {
            raw.Append(
                CultureInfo.InvariantCulture,
                $"tests/t{f}.rs:{f + 1}:parse_config(A)\ntests/t{f}.rs:{f + 30}:parse_config(B)\n"
            );
        }

        return Verify(Grep.Group(Argv(cmd), raw.ToString())).UseParameters(cmd.Split(' ')[0]);
    }
}
