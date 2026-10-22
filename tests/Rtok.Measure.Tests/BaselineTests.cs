using Rtok.Config;
using Rtok.Testing;
using static Rtok.Measure.Tests.Lines;

namespace Rtok.Measure.Tests;

public sealed class BaselineTests
{
    [Fact]
    public void Save_then_compare_is_all_zero_deltas()
    {
        using var home = new TempDir();
        using var logs = new TempDir();
        Write(
            logs,
            "s.jsonl",
            [Use("t1", "Bash", Obj(("command", "git status"))), Result("t1", "clean")]
        );
        var json = StatsJson.Render(Collect(logs));

        var path = Baseline.Save(home.Path, "before-rtok", json);
        var text = Baseline.Compare(home.Path, "before-rtok", json);

        Assert.Equal(home.Combine("measurements", "before-rtok.json"), path);
        Assert.Contains("tools.Bash.est_tokens 2 Δ0", text, StringComparison.Ordinal);
        Assert.Contains("bash.git.ctt", text, StringComparison.Ordinal);
        Assert.All(
            text.TrimEnd('\n').Split('\n'),
            l => Assert.EndsWith("Δ0", l, StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Compare_counts_a_row_only_one_side_has_as_zero_on_the_other()
    {
        using var home = new TempDir();
        Baseline.Save(
            home.Path,
            "old",
            """{"sessions":1,"tools":{"Read":{"est_tokens":10,"ctt":30}}}"""
        );
        var text = Baseline.Compare(
            home.Path,
            "old",
            """{"sessions":3,"tools":{"Bash":{"est_tokens":4,"ctt":8}}}"""
        );
        Assert.Equal(
            "sessions 3 Δ2\nlines 0 Δ0\nusage_input 0 Δ0\nusage_output 0 Δ0\n"
                + "tools.Bash.est_tokens 4 Δ4\ntools.Bash.ctt 8 Δ8\n"
                + "tools.Read.est_tokens 0 Δ-10\ntools.Read.ctt 0 Δ-30\n",
            text
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData(@"a\b")]
    public void A_name_with_a_folder_is_refused(string name)
    {
        using var home = new TempDir();
        Assert.Throws<InvalidOperationException>(() => Baseline.Save(home.Path, name, "{}"));
    }

    [Fact]
    public void An_unknown_baseline_names_its_path()
    {
        using var home = new TempDir();
        var e = Assert.Throws<InvalidOperationException>(() =>
            Baseline.Compare(home.Path, "nope", "{}")
        );
        Assert.Contains(
            home.Combine("measurements", "nope.json"),
            e.Message,
            StringComparison.Ordinal
        );
    }
}
