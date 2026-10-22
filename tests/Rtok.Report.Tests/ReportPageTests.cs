using Rtok.Config;

namespace Rtok.Report.Tests;

/// <summary>The command's flags against <c>[report]</c>, and where the output goes.</summary>
public sealed class ReportPageTests
{
    [Fact]
    public void An_explicit_format_beats_the_config()
    {
        using var fixture = new ReportFixture(seed: false);
        File.AppendAllText(fixture.UserFile, "[report]\nformat = \"html\"\n");

        var fromConfig = ReportPage.Renderer(fixture.Load().Config, NewPdf);
        var fromFlag = ReportPage.Renderer(
            fixture.Load([.. ReportPage.Flags("md", null, null, ai: false)]).Config,
            NewPdf
        );
        var ai = ReportPage.Renderer(
            fixture.Load([.. ReportPage.Flags("md", null, null, ai: true)]).Config,
            NewPdf
        );

        Assert.Equal(("html", "md", "ai"), (fromConfig.Format, fromFlag.Format, ai.Format));
    }

    [Theory]
    [InlineData("5x")]
    [InlineData("-1d")]
    [InlineData("")]
    public void A_bad_since_is_rejected(string since)
    {
        using var fixture = new ReportFixture(seed: false);
        var flags = ReportPage.Flags(null, null, since, ai: false);

        var e = Assert.Throws<ConfigException>(() =>
            ReportPage.Build(fixture.Load([.. flags]), fixture.Clock.GetUtcNow(), "")
        );

        Assert.Contains("report.since", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_since_flag_sets_the_window()
    {
        using var fixture = new ReportFixture();

        var document = fixture.Document([.. ReportPage.Flags(null, null, "60d", ai: false)]);

        Assert.Equal(TimeSpan.FromDays(60), document.Window.To - document.Window.From);
        Assert.Equal("60d", document.Window.Since);
    }

    [Fact]
    public void Out_writes_to_the_given_path_and_names_it()
    {
        using var fixture = new ReportFixture();
        using var dir = new Rtok.Testing.TempDir("rtok-report-out-");
        var path = dir.Combine("r.md");
        using var stdout = new MemoryStream();

        var line = ReportPage.Write(fixture.Document(), new MarkdownRenderer(), path, stdout);

        Assert.Equal(path + "\n", line);
        Assert.Equal(0, stdout.Length);
        Assert.StartsWith("# rtok report\n", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_out_writes_to_stdout()
    {
        using var fixture = new ReportFixture(seed: false);
        using var stdout = new MemoryStream();

        var line = ReportPage.Write(fixture.Document(), new MarkdownRenderer(), "", stdout);

        Assert.Null(line);
        Assert.NotEqual(0, stdout.Length);
    }

    private static IReportRenderer NewPdf() => new Pdf.PdfRenderer();
}
