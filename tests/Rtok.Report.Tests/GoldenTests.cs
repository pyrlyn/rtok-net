using System.Text;
using Rtok.Config;
using Rtok.Measure;

namespace Rtok.Report.Tests;

/// <summary>The three text forms of the fixture store's report, byte for byte.</summary>
public sealed class GoldenTests
{
    [Fact]
    public async Task Markdown_shows_every_section_with_its_evidence()
    {
        using var fixture = new ReportFixture();

        await Verify(fixture.Scrub(MarkdownRenderer.Text(Pinned(fixture.Document()))));
    }

    [Fact]
    public async Task Html_shows_every_section_with_its_evidence()
    {
        using var fixture = new ReportFixture();

        await Verify(fixture.Scrub(Render(new HtmlRenderer(), Pinned(fixture.Document()))), "html");
    }

    [Fact]
    public async Task Ai_has_toon_tables_anchors_units_and_a_task_list()
    {
        using var fixture = new ReportFixture();
        var loaded = fixture.Load(new ConfigFlag("report.ai", true, "--ai"));
        var renderer = ReportPage.Renderer(
            loaded.Config,
            () => throw new InvalidOperationException()
        );

        var text = Render(
            renderer,
            Pinned(fixture.Document(new ConfigFlag("report.ai", true, "--ai")))
        );

        Assert.IsType<AiRenderer>(renderer);
        Assert.Contains("#recommendations\n", text, StringComparison.Ordinal);
        Assert.Contains("1. [", text, StringComparison.Ordinal);
        await Verify(fixture.Scrub(text));
    }

    [Fact]
    public void The_fixture_fires_every_rule()
    {
        using var fixture = new ReportFixture();
        var loaded = fixture.Load();
        var facts = ReportFacts.Read(
            fixture.Store,
            fixture.Document().Window,
            AdviceSettings.Of(loaded.Config)
        );

        Assert.Equal(
            [
                "archive-window",
                "cache-bust",
                "expand-rate",
                "idle-hook",
                "inject-budget",
                "retire-plugin",
                "top-sinks",
            ],
            Advice
                .Of(facts)
                .Select(f => f.Rule)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
            StringComparer.Ordinal
        );
    }

    // Every new setting in another task would move a golden that lists the defaults: the goldens
    // keep the config rows the fixture set and drop the key count with them.
    private static ReportDocument Pinned(ReportDocument document) =>
        document with
        {
            Sections =
            [
                .. document.Sections.Select(s =>
                    s.Id is not "config"
                        ? s
                        : s with
                        {
                            Blocks =
                            [
                                s.Blocks[0],
                                s
                                    .Blocks.OfType<Table>()
                                    .Select(t =>
                                        t with
                                        {
                                            Rows = [.. t.Rows.Where(r => r[2] is not "default")],
                                        }
                                    )
                                    .Single(),
                            ],
                        }
                ),
            ],
        };

    internal static string Render(IReportRenderer renderer, ReportDocument document)
    {
        using var stream = new MemoryStream();
        renderer.Render(document, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
