using System.Text;
using System.Text.RegularExpressions;
using Rtok.Report;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any strings, NUL-separated, in every slot a store row can reach (title, window, section,
/// spans, table cells, code, chart labels, findings): the html opens only the renderer's own
/// tags, and the markdown keeps its headings and table columns outside the code fences.
/// </summary>
internal static partial class ReportHtml
{
    private static readonly HashSet<string> Tags = new(StringComparer.Ordinal)
    {
        "!DOCTYPE",
        "html",
        "head",
        "meta",
        "title",
        "style",
        "body",
        "h1",
        "h2",
        "p",
        "code",
        "strong",
        "section",
        "table",
        "thead",
        "tbody",
        "tr",
        "th",
        "td",
        "pre",
        "ol",
        "li",
        "svg",
        "rect",
        "text",
    };

    public static void Run(ReadOnlySpan<byte> data)
    {
        var p = FuzzInput.Parts(data, 5);
        var document = new ReportDocument(
            p[0],
            new ReportWindow(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, p[1], p[2]),
            [
                new ReportSection(
                    p[0],
                    p[1],
                    p[2],
                    [
                        new Paragraph([
                            new Span(p[3]),
                            new Span(p[4], SpanKind.Code),
                            new Span(p[0], SpanKind.Strong),
                        ]),
                        new Table(
                            [new(p[1]), new(p[2], Numeric: true)],
                            [
                                [p[3], p[4]],
                            ]
                        ),
                        new CodeBlock(p[4]),
                        new BarChart(p[0], [new Bar(p[3], 1), new Bar(p[4], -1)]),
                        new Findings([new Finding(p[0], p[1], p[2], 1)]),
                    ]
                ),
            ]
        );
        Html(Render(new HtmlRenderer(), document));
        Markdown(MarkdownRenderer.Text(document));
    }

    private static void Html(string html)
    {
        foreach (Match tag in Tag().Matches(html))
        {
            FuzzInput.Check(
                Tags.Contains(tag.Groups["name"].Value),
                $"html opens <{tag.Groups["name"].Value}>"
            );
        }
        FuzzInput.Check(
            Regex.Count(html, "<section ", RegexOptions.None, TimeSpan.FromSeconds(1)) == 1,
            "html has a second section"
        );
    }

    private static void Markdown(string md)
    {
        var fence = 0;
        var headings = 0;
        foreach (var line in md.Split('\n'))
        {
            var ticks = line.Length - line.TrimStart('`').Length;
            if (fence == 0 && ticks >= 3)
            {
                fence = ticks;
                continue;
            }
            if (fence > 0)
            {
                fence = ticks >= fence && line.Trim('`').Length == 0 ? 0 : fence;
                continue;
            }
            headings += line.StartsWith('#') ? 1 : 0;
            if (line.StartsWith("| ", StringComparison.Ordinal))
            {
                FuzzInput.Check(Pipes(line) == 3, $"markdown table row with extra columns: {line}");
            }
        }
        FuzzInput.Check(fence == 0, "markdown fence left open");
        FuzzInput.Check(headings == 2, $"markdown has {headings} headings");
    }

    // Pipes not escaped by an odd run of backslashes.
    private static int Pipes(string line)
    {
        var count = 0;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] != '|')
            {
                continue;
            }
            var slashes = 0;
            for (var j = i - 1; j >= 0 && line[j] == '\\'; j--)
            {
                slashes++;
            }
            count += slashes % 2 == 0 ? 1 : 0;
        }
        return count;
    }

    private static string Render(HtmlRenderer renderer, ReportDocument document)
    {
        using var stream = new MemoryStream();
        renderer.Render(document, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [GeneratedRegex(
        "</?(?<name>[^\\s>/]*)",
        RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex Tag();
}
