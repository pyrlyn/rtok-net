using System.Globalization;
using System.Text;

namespace Rtok.Report;

/// <summary>The report as one self-contained HTML page: inline CSS, inline SVG bars, a stable
/// id per section and nothing fetched from anywhere. Every piece of data is escaped.</summary>
public sealed class HtmlRenderer : IReportRenderer
{
    /// <summary>Chart width in pixels.</summary>
    public const int ChartWidth = 600;

    /// <summary>The longest bar in pixels; the label column sits left of it.</summary>
    public const int BarSpan = 440;

    private const int BarX = 140;
    private const int Row = 24;

    private const string Css =
        "body{font-family:sans-serif;max-width:60rem;margin:2rem auto;padding:0 1rem;color:#222}"
        + "table{border-collapse:collapse;margin:1rem 0}"
        + "th,td{border:1px solid #ccc;padding:.25rem .5rem;text-align:left}"
        + "td.n,th.n{text-align:right}"
        + "pre{background:#f6f6f6;padding:1rem;overflow-x:auto}"
        + "svg.chart{background:#fafafa;margin:1rem 0}";

    /// <inheritdoc />
    public string Format => "html";

    /// <inheritdoc />
    public void Render(ReportDocument document, Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.Write(Encoding.UTF8.GetBytes(Text(document)));
    }

    /// <summary>The page, ending in a newline.</summary>
    public static string Text(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var w = document.Window;
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<title>")
            .Append(Escape(document.Title))
            .Append("</title>\n<style>")
            .Append(Css)
            .Append("</style>\n</head>\n<body>\n<h1>")
            .Append(Escape(document.Title))
            .Append("</h1>\n<p>Window ")
            .Append(w.FromDate)
            .Append(" → ")
            .Append(w.ToDate)
            .Append(" (")
            .Append(Escape(w.Since))
            .Append(") · store <code>")
            .Append(Escape(w.Store))
            .Append("</code></p>\n");
        foreach (var section in document.Sections)
        {
            sb.Append("<section id=\"")
                .Append(Escape(section.Id))
                .Append("\">\n<h2>")
                .Append(Escape(section.Title))
                .Append("</h2>\n");
            foreach (var block in section.Blocks)
            {
                Block(sb, block);
            }
            sb.Append("</section>\n");
        }
        return sb.Append("</body>\n</html>\n").ToString();
    }

    private static void Block(StringBuilder sb, ReportBlock block)
    {
        switch (block)
        {
            case Paragraph p:
                sb.Append("<p>").Append(Spans(p.Spans)).Append("</p>\n");
                break;
            case Table t:
                Table(sb, t);
                break;
            case CodeBlock c:
                sb.Append("<pre>").Append(Escape(c.Text)).Append("</pre>\n");
                break;
            case BarChart c:
                Chart(sb, c);
                break;
            case Findings { Items.Count: 0 }:
                sb.Append("<p>").Append(Findings.None).Append("</p>\n");
                break;
            case Findings f:
                sb.Append("<ol>\n");
                foreach (var i in f.Items)
                {
                    sb.Append("<li><strong>")
                        .Append(Escape(i.Rule))
                        .Append("</strong>: ")
                        .Append(Escape(i.Text))
                        .Append(" (")
                        .Append(Escape(i.Evidence))
                        .Append(")</li>\n");
                }
                sb.Append("</ol>\n");
                break;
        }
    }

    private static string Spans(IEnumerable<Span> spans) =>
        string.Concat(
            spans.Select(s =>
                s.Kind switch
                {
                    SpanKind.Code => "<code>" + Escape(s.Text) + "</code>",
                    SpanKind.Strong => "<strong>" + Escape(s.Text) + "</strong>",
                    _ => Escape(s.Text),
                }
            )
        );

    private static void Table(StringBuilder sb, Table t)
    {
        sb.Append("<table>\n<thead><tr>");
        foreach (var c in t.Columns)
        {
            sb.Append(c.Numeric ? "<th class=\"n\">" : "<th>")
                .Append(Escape(c.Name))
                .Append("</th>");
        }
        sb.Append("</tr></thead>\n<tbody>\n");
        foreach (var row in t.Rows)
        {
            sb.Append("<tr>");
            for (var i = 0; i < row.Count; i++)
            {
                var numeric = i < t.Columns.Count && t.Columns[i].Numeric;
                sb.Append(numeric ? "<td class=\"n\">" : "<td>")
                    .Append(Escape(row[i]))
                    .Append("</td>");
            }
            sb.Append("</tr>\n");
        }
        sb.Append("</tbody>\n</table>\n");
    }

    private static void Chart(StringBuilder sb, BarChart chart)
    {
        if (chart.Bars.Count == 0)
        {
            sb.Append("<p>No data for ").Append(Escape(chart.Title)).Append(".</p>\n");
            return;
        }
        var height = Saturating.ToInt((double)chart.Bars.Count * Row + 8);
        sb.Append(
            CultureInfo.InvariantCulture,
            $"<svg class=\"chart\" width=\"{ChartWidth}\" height=\"{height}\" role=\"img\" aria-label=\"{Escape(chart.Title)}\">\n"
        );
        for (var i = 0; i < chart.Bars.Count; i++)
        {
            var bar = chart.Bars[i];
            var y = (i * Row) + 4;
            var width = BarWidth(chart, bar);
            sb.Append(
                CultureInfo.InvariantCulture,
                $"<text x=\"0\" y=\"{y + 12}\" font-size=\"12\">{Escape(Label(bar.Label))}</text>"
                    + $"<rect x=\"{BarX}\" y=\"{y}\" width=\"{width}\" height=\"14\" fill=\"#4a7bd0\"/>"
                    + $"<text x=\"{BarX + 6 + width}\" y=\"{y + 12}\" font-size=\"12\">{Fmt.Num(bar.Value)}</text>\n"
            );
        }
        sb.Append("</svg>\n");
    }

    /// <summary>A bar's width in pixels on its chart's one scale.</summary>
    public static int BarWidth(BarChart chart, Bar bar)
    {
        ArgumentNullException.ThrowIfNull(chart);
        return Saturating.ToInt(chart.Share(bar) * BarSpan);
    }

    private static string Label(string label)
    {
        if (label.Length <= 20)
        {
            return label;
        }
        var cut = char.IsHighSurrogate(label[18]) ? 18 : 19;
        return label[..cut] + "…";
    }

    /// <summary>Text and attribute data: <c>&amp; &lt; &gt; " '</c> escaped.</summary>
    internal static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(
                c switch
                {
                    '&' => "&amp;",
                    '<' => "&lt;",
                    '>' => "&gt;",
                    '"' => "&quot;",
                    '\'' => "&#39;",
                    _ => c.ToString(),
                }
            );
        }
        return sb.ToString();
    }
}
