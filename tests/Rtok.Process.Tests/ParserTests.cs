using System.Text;

namespace Rtok.Process.Tests;

public sealed class ParserTests
{
    [Theory]
    [InlineData(
        "Receiving objects:  45% (120/267), 1.2 MiB | 3 MiB/s",
        120L,
        267L,
        "Receiving objects"
    )]
    [InlineData("remote: Counting objects: 100% (5/5), done.", 5L, 5L, "Counting objects")]
    [InlineData("Updating files: 50% (1/2)", 1L, 2L, "Updating files")]
    public void Git_reads_the_count_and_the_phase(
        string line,
        long done,
        long total,
        string phase
    ) =>
        Assert.Equal(
            new ProgressUpdate(done, total, ProgressUnit.Items, phase),
            new GitProgress().Feed(ChildOutput.Stderr, line)
        );

    [Fact]
    public void Git_falls_back_to_the_percent_and_skips_other_lines()
    {
        var git = new GitProgress();

        Assert.Equal(
            new ProgressUpdate(30, 100, ProgressUnit.Percent, "Resolving deltas"),
            git.Feed(ChildOutput.Stderr, "Resolving deltas:  30%")
        );
        Assert.Null(git.Feed(ChildOutput.Stderr, "Cloning into 'x'..."));
        Assert.Null(git.Feed(ChildOutput.Stderr, "odd: 250% (1/2)"));
    }

    [Theory]
    [InlineData("[42%] building", 42L)]
    [InlineData("done 100%", 100L)]
    [InlineData("0%", 0L)]
    public void Percent_reads_the_first_percentage(string line, long value) =>
        Assert.Equal(
            new ProgressUpdate(value, 100, ProgressUnit.Percent, null),
            new PercentProgress().Feed(ChildOutput.Stdout, line)
        );

    [Theory]
    [InlineData("no number here")]
    [InlineData("%")]
    [InlineData("101%")]
    public void Percent_skips_lines_without_one(string line) =>
        Assert.Null(new PercentProgress().Feed(ChildOutput.Stdout, line));

    [Fact]
    public void Cargo_reads_its_bar_and_the_crates_being_built()
    {
        var cargo = new CargoProgress();

        Assert.Equal(
            new ProgressUpdate(12, 98, ProgressUnit.Items, "serde, tokio"),
            cargo.Feed(ChildOutput.Stderr, "    Building [=====>      ] 12/98: serde, tokio")
        );
        Assert.Equal(
            new ProgressUpdate(3, 4, ProgressUnit.Items, null),
            cargo.Feed(ChildOutput.Stderr, "    Building [=========> ] 3/4")
        );
        Assert.Null(cargo.Feed(ChildOutput.Stderr, "   Compiling serde v1.0.0"));
        Assert.Null(cargo.Feed(ChildOutput.Stderr, "[warn] 1/2: x"));
        Assert.Null(cargo.Feed(ChildOutput.Stderr, "    Building [===] 0/0"));
        Assert.Contains(("CARGO_TERM_PROGRESS_WHEN", "always"), CargoProgress.Environment);
    }

    [Fact]
    public void Json_lines_read_the_progress_object()
    {
        var json = new JsonLinesProgress();

        Assert.Equal(
            new ProgressUpdate(3, 10, ProgressUnit.Bytes, "x"),
            json.Feed(
                ChildOutput.Stdout,
                """{"progress":{"pos":3,"total":10,"unit":"bytes","msg":"x"}}"""
            )
        );
        Assert.Equal(
            new ProgressUpdate(1, null, ProgressUnit.Items, null),
            json.Feed(ChildOutput.Stdout, """  {"progress":{"pos":1}}""")
        );
    }

    [Theory]
    [InlineData("""{"progress":{"pos":1,"unit":"lines"}}""")]
    [InlineData("""{"progress":{"pos":-1}}""")]
    [InlineData("""{"progress":{"pos":"1"}}""")]
    [InlineData("""{"other":1}""")]
    [InlineData("""{"progress":""")]
    [InlineData("plain text")]
    public void Json_lines_skip_anything_else(string line) =>
        Assert.Null(new JsonLinesProgress().Feed(ChildOutput.Stdout, line));

    [Fact]
    public void Lines_split_on_newlines_and_carriage_returns_across_chunks()
    {
        var lines = new List<string>();
        var splitter = new LineSplitter();

        splitter.Push("a\r\nb"u8, lines.Add);
        splitter.Push("c\rd"u8, lines.Add);
        splitter.Finish(lines.Add);

        Assert.Equal(["a", "bc", "d"], lines);
    }

    [Fact]
    public void A_line_longer_than_the_cap_is_cut()
    {
        var lines = new List<string>();
        var splitter = new LineSplitter();

        splitter.Push(
            Encoding.ASCII.GetBytes(new string('x', LineSplitter.LineCap + 10) + "\n"),
            lines.Add
        );

        Assert.Equal(LineSplitter.LineCap, Assert.Single(lines).Length);
    }

    [Fact]
    public void The_budget_is_shared_and_the_rest_is_counted()
    {
        var pump = new Pump(HiddenIndicator.Instance, null, 10, forward: false);

        Assert.Equal((6, 4, 0), (pump.Take(6), pump.Take(6), pump.Take(3)));
        Assert.Equal(5, pump.Dropped);
    }
}
