using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Rtok.Report.Pdf.Tests;

/// <summary>The report PDF read back with PdfPig: page size, fonts, contents page, bookmarks,
/// text and page breaks are what a reader gets, not what the layout code meant.</summary>
public sealed class PdfRendererTests
{
    private static readonly ReportWindow Window = new(
        DateTimeOffset.FromUnixTimeSeconds(1_797_408_000),
        DateTimeOffset.FromUnixTimeSeconds(1_800_000_000),
        "30d",
        "/home/u/.rtok/rtok.db"
    );

    [Fact]
    public void The_licence_type_is_set_before_the_first_document()
    {
        _ = new PdfRenderer();

        Assert.Equal("Community", PdfRenderer.License);
    }

    [Fact]
    public void Pages_are_A4_in_one_font_family()
    {
        using var pdf = Open(Document());

        Assert.All(
            pdf.GetPages(),
            p => Assert.Equal((595, 842), ((int)Math.Round(p.Width), (int)Math.Round(p.Height)))
        );
        var fonts = pdf.GetPages()
            .SelectMany(p => p.Letters)
            .Select(l => l.FontName)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Assert.NotEmpty(fonts);
        Assert.All(fonts, f => Assert.Contains(PdfRenderer.Font, f, StringComparison.Ordinal));
    }

    [Fact]
    public void The_contents_page_names_the_physical_page_of_each_section()
    {
        var document = Document();
        using var pdf = Open(document);
        var contents = Lines(pdf.GetPage(1));

        foreach (var section in document.Sections)
        {
            var page = pdf.GetPages()
                .Skip(1)
                .First(p => Lines(p).Contains(section.Title, StringComparer.Ordinal))
                .Number;
            Assert.Contains($"{section.Title} {page}", contents, StringComparer.Ordinal);
        }
        Assert.True(pdf.NumberOfPages > 2);
    }

    [Fact]
    public void Each_section_has_one_bookmark()
    {
        var document = Document();
        using var pdf = Open(document);

        Assert.True(pdf.TryGetBookmarks(out var bookmarks));
        var titles = bookmarks.GetNodes().Select(n => n.Title).ToList();
        Assert.Equal(
            document.Sections.Select(s => s.Title),
            titles.Where(t => !string.Equals(t, document.Title, StringComparison.Ordinal)),
            StringComparer.Ordinal
        );
    }

    [Fact]
    public void Utf8_text_comes_back_intact()
    {
        const string Text = "naïve café — Größe, ½ ok";
        var document = Document(new Paragraph([new Span(Text)]));
        using var pdf = Open(document);

        Assert.Contains(pdf.GetPages(), p => p.Text.Contains(Text, StringComparison.Ordinal));
    }

    [Fact]
    public void Text_without_a_glyph_does_not_stop_the_document()
    {
        var bytes = Render(Document(new Paragraph([new Span("日本語 🙂")])));

        Assert.StartsWith(
            "%PDF-",
            System.Text.Encoding.ASCII.GetString(bytes, 0, 5),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void An_orphan_heading_moves_with_its_block()
    {
        // Filler that leaves the next heading a line or two above the page foot.
        for (var lines = 60; lines < 100; lines++)
        {
            var document = new ReportDocument(
                "rtok report",
                Window,
                [
                    new ReportSection(
                        "a",
                        "Alpha",
                        "units",
                        [new CodeBlock(string.Concat(Enumerable.Repeat("filler line\n", lines)))]
                    ),
                    new ReportSection(
                        "b",
                        "Bravo",
                        "units",
                        [new Paragraph([new Span("bravo body first line")])]
                    ),
                ]
            );
            using var pdf = Open(document);
            var heading = pdf.GetPages().Skip(1).First(p => Lines(p).Contains("Bravo")).Number;
            var body = pdf.GetPages()
                .Skip(1)
                .First(p => p.Text.Contains("bravo body first line", StringComparison.Ordinal))
                .Number;
            Assert.Equal(body, heading);
        }
    }

    private static ReportDocument Document(params ReportBlock[] extra)
    {
        var rows = Enumerable
            .Range(0, 60)
            .Select(i => (IReadOnlyList<string>)[$"plugins.key{i}", $"value {i}", "default"])
            .ToList();
        return new ReportDocument(
            "rtok report",
            Window,
            [
                new ReportSection(
                    "window",
                    "Window",
                    "units: rows",
                    [new Paragraph([new Span("Rows "), new Span("12", SpanKind.Strong)])]
                ),
                new ReportSection(
                    "savings",
                    "Savings",
                    "units: tokens",
                    [
                        new BarChart(
                            "saved tokens per plugin",
                            [new Bar("cmd", 9_000), new Bar("read", 1_500), new Bar("graph", -200)]
                        ),
                    ]
                ),
                new ReportSection(
                    "config",
                    "Config",
                    "units: keys",
                    [new Table([new("key"), new("value"), new("source")], rows), .. extra]
                ),
                new ReportSection(
                    "doctor",
                    "Doctor",
                    "units: probes",
                    [new CodeBlock("hooks: 2\n")]
                ),
                new ReportSection(
                    "recommendations",
                    "Recommendations",
                    "units: rules",
                    [
                        new Findings([
                            new Finding(
                                "retire-plugin",
                                "plugin graph net -200",
                                "1 graph Measurement row",
                                200
                            ),
                        ]),
                    ]
                ),
            ]
        );
    }

    private static byte[] Render(ReportDocument document)
    {
        using var stream = new MemoryStream();
        new PdfRenderer().Render(document, stream);
        return stream.ToArray();
    }

    private static PdfDocument Open(ReportDocument document) => PdfDocument.Open(Render(document));

    // Words grouped into lines by their baseline, then joined with single spaces.
    private static List<string> Lines(Page page) =>
        [
            .. page.GetWords()
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom))
                .OrderByDescending(g => g.Key)
                .Select(g =>
                    string.Join(' ', g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text))
                ),
        ];
}
