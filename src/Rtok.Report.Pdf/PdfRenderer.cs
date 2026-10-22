using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Rtok.Report.Pdf;

/// <summary>
/// The report as an A4 PDF on QuestPDF (README N18): a contents page with the physical page of
/// each section, one bookmark per section, tables, and native bars on each chart's one scale.
/// One font family, the Lato QuestPDF ships, with system fonts off, so the output does not
/// depend on the machine. A heading never stays behind at a page foot: it moves with the start
/// of its block. No QuestPDF type leaves this project.
/// </summary>
public sealed class PdfRenderer : IReportRenderer
{
    /// <summary>The one font family.</summary>
    public const string Font = "Lato";

    private const float BarSpan = 300;

    // The minimum room a heading needs below it: the heading and the first lines of its block.
    private const float HeadingKeep = 90;

    static PdfRenderer()
    {
        // Set before the first document: QuestPDF refuses to generate without a licence type.
        Settings.License = LicenseType.Community;
        Settings.UseSystemFonts = false;
        // A store string in a script Lato lacks prints as a missing glyph, not an error.
        Settings.ThrowOnMissingTextGlyphs = false;
    }

    /// <summary>The licence type QuestPDF runs under.</summary>
    public static string License => Settings.License?.ToString() ?? "";

    /// <inheritdoc />
    public string Format => "pdf";

    /// <inheritdoc />
    public void Render(ReportDocument document, Stream output)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(output);
        Document
            .Create(c => Compose(c, document))
            .WithMetadata(
                new DocumentMetadata
                {
                    Title = document.Title,
                    Creator = "rtok",
                    Producer = "rtok",
                    CreationDate = document.Window.To,
                    ModifiedDate = document.Window.To,
                }
            )
            // PDF/UA tags the headings, and the tagged headings become the bookmarks.
            .WithSettings(new DocumentSettings { PDFUA_Conformance = PDFUA_Conformance.PDFUA_1 })
            .GeneratePdf(output);
    }

    private static void Compose(IDocumentContainer container, ReportDocument document) =>
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(t => t.FontFamily(Font).FontSize(9));
            page.Footer()
                .AlignCenter()
                .Text(t =>
                {
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            page.Content()
                .Column(col =>
                {
                    Contents(col, document);
                    foreach (var section in document.Sections)
                    {
                        col.Item().Section(section.Id).Column(s => Section(s, section));
                    }
                });
        });

    private static void Contents(ColumnDescriptor col, ReportDocument document)
    {
        var w = document.Window;
        col.Item().SemanticHeading1().Text(document.Title).FontSize(18).Bold();
        col.Item()
            .PaddingBottom(12)
            .Text($"Window {w.FromDate} → {w.ToDate} ({w.Since}) · store {w.Store}");
        col.Item().Text("Contents").FontSize(13).Bold();
        foreach (var section in document.Sections)
        {
            col.Item()
                .SectionLink(section.Id)
                .Row(row =>
                {
                    row.RelativeItem().Text(section.Title);
                    row.AutoItem().Text(t => t.BeginPageNumberOfSection(section.Id));
                });
        }
        col.Item().PageBreak();
    }

    private static void Section(ColumnDescriptor col, ReportSection section)
    {
        col.Spacing(6);
        col.Item()
            .EnsureSpace(HeadingKeep)
            .Column(head =>
            {
                head.Item()
                    .PaddingTop(10)
                    .SemanticHeading2()
                    .Text(section.Title)
                    .FontSize(13)
                    .Bold();
                if (section.Blocks.Count > 0)
                {
                    Block(head.Item(), section.Blocks[0]);
                }
            });
        foreach (var block in section.Blocks.Skip(1))
        {
            Block(col.Item(), block);
        }
    }

    private static void Block(IContainer item, ReportBlock block)
    {
        switch (block)
        {
            case Paragraph p:
                item.Text(t =>
                {
                    foreach (var span in p.Spans)
                    {
                        var s = t.Span(span.Text);
                        _ = span.Kind switch
                        {
                            SpanKind.Strong => s.Bold(),
                            SpanKind.Code => s.BackgroundColor(Colors.Grey.Lighten4),
                            _ => s,
                        };
                    }
                });
                break;
            case Table t:
                Table(item, t);
                break;
            case CodeBlock c:
                item.Background(Colors.Grey.Lighten4).Padding(6).Text(c.Text).FontSize(7.5f);
                break;
            case BarChart c:
                Chart(item, c);
                break;
            case Findings { Items.Count: 0 }:
                item.Text(Findings.None);
                break;
            case Findings f:
                item.Column(list =>
                {
                    for (var i = 0; i < f.Items.Count; i++)
                    {
                        var finding = f.Items[i];
                        list.Item()
                            .Text(t =>
                            {
                                t.Span($"{i + 1}. ");
                                t.Span(finding.Rule).Bold();
                                t.Span($": {finding.Text} ({finding.Evidence})");
                            });
                    }
                });
                break;
        }
    }

    private static void Table(IContainer item, Table table) =>
        item.Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                foreach (var _ in table.Columns)
                {
                    c.RelativeColumn();
                }
            });
            t.Header(h =>
            {
                foreach (var column in table.Columns)
                {
                    Cell(h.Cell(), column.Numeric).Text(column.Name).Bold();
                }
            });
            foreach (var row in table.Rows)
            {
                for (var i = 0; i < table.Columns.Count; i++)
                {
                    Cell(t.Cell(), table.Columns[i].Numeric).Text(i < row.Count ? row[i] : "");
                }
            }
        });

    private static IContainer Cell(IContainer cell, bool numeric)
    {
        var padded = cell.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2);
        return numeric ? padded.AlignRight() : padded;
    }

    private static void Chart(IContainer item, BarChart chart)
    {
        if (chart.Bars.Count == 0)
        {
            item.Text($"No data for {chart.Title}.");
            return;
        }
        item.Column(col =>
        {
            col.Item().Text(chart.Title).Italic();
            foreach (var bar in chart.Bars)
            {
                // Each row is laid out at its drawn height, so a page break never cuts a bar.
                col.Item()
                    .ShowEntire()
                    .Row(row =>
                    {
                        row.ConstantItem(110).Text(bar.Label).ClampLines(1);
                        var width = (float)(chart.Share(bar) * BarSpan);
                        if (width > 0)
                        {
                            row.ConstantItem(width)
                                .PaddingVertical(2)
                                .Height(8)
                                .Background(Colors.Blue.Medium);
                        }
                        row.RelativeItem().PaddingLeft(4).Text(Fmt.Num(bar.Value));
                    });
            }
        });
    }
}
