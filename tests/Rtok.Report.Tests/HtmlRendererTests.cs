using System.Text.RegularExpressions;
using Rtok.Report.Pdf;

namespace Rtok.Report.Tests;

/// <summary>The html page stands alone, and every renderer reads the one model.</summary>
public sealed partial class HtmlRendererTests
{
    [Fact]
    public void The_page_is_self_contained_with_stable_ids()
    {
        using var fixture = new ReportFixture();
        var document = fixture.Document();

        var html = GoldenTests.Render(new HtmlRenderer(), document);

        Assert.Contains("<style>", html, StringComparison.Ordinal);
        Assert.Contains("<svg", html, StringComparison.Ordinal);
        // Store and config strings may name URLs as text; the markup itself loads nothing.
        var markup = TextNodes().Replace(html, "><");
        Assert.DoesNotContain("http:", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https:", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("url(", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(External(), markup);
        Assert.Equal(
            document.Sections.Select(s => s.Id),
            SectionIds().Matches(html).Select(m => m.Groups["id"].Value),
            StringComparer.Ordinal
        );
    }

    [Fact]
    public void Bars_of_one_chart_share_one_scale()
    {
        var chart = new BarChart("c", [new Bar("a", 400), new Bar("b", 100), new Bar("c", -5)]);

        Assert.Equal(400, chart.Scale);
        Assert.Equal([440, 110, 0], chart.Bars.Select(b => HtmlRenderer.BarWidth(chart, b)));
        Assert.Equal(1, new BarChart("z", [new Bar("a", 0)]).Scale);
    }

    [Fact]
    public void Store_strings_are_escaped()
    {
        var document = new ReportDocument(
            "<t>",
            Facts.Window,
            [
                new ReportSection(
                    "s",
                    "S",
                    "u",
                    [
                        new Paragraph([new Span("<script>alert('x')</script> & \"q\"")]),
                        new Table(
                            [new("k")],
                            [
                                ["<img src=x>"],
                            ]
                        ),
                    ]
                ),
            ]
        );

        var html = GoldenTests.Render(new HtmlRenderer(), document);

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(&#39;x&#39;)", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Md_html_and_pdf_render_the_same_instance()
    {
        using var fixture = new ReportFixture();
        var document = fixture.Document();
        var seen = new List<ReportDocument>();
        IReportRenderer[] renderers =
        [
            new MarkdownRenderer(),
            new HtmlRenderer(),
            new PdfRenderer(),
        ];

        foreach (var renderer in renderers)
        {
            var spy = new Spy(renderer, seen);
            Assert.NotEmpty(GoldenTests.Render(spy, document));
        }

        Assert.Equal(
            ["md", "html", "pdf"],
            renderers.Select(r => r.Format),
            StringComparer.Ordinal
        );
        Assert.All(seen, d => Assert.Same(document, d));
        Assert.Equal(3, seen.Count);
    }

    [GeneratedRegex(
        """(?:src|href)\s*=""",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex External();

    [GeneratedRegex(
        """<section id="(?<id>[^"]+)">""",
        RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex SectionIds();

    [GeneratedRegex(">[^<]*<", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TextNodes();

    private sealed class Spy(IReportRenderer inner, List<ReportDocument> seen) : IReportRenderer
    {
        public string Format => inner.Format;

        public void Render(ReportDocument document, Stream output)
        {
            seen.Add(document);
            inner.Render(document, output);
        }
    }
}
