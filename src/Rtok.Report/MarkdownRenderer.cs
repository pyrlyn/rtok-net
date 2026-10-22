using System.Text;

namespace Rtok.Report;

/// <summary>The report as Markdown. Every piece of data is escaped, so a store string never
/// becomes markup; charts are left to the tables beside them.</summary>
public sealed class MarkdownRenderer : IReportRenderer
{
    /// <inheritdoc />
    public string Format => "md";

    /// <inheritdoc />
    public void Render(ReportDocument document, Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.Write(Encoding.UTF8.GetBytes(Text(document)));
    }

    /// <summary>The Markdown text, ending in a newline.</summary>
    public static string Text(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var w = document.Window;
        var sb = new StringBuilder();
        sb.Append("# ").Append(Escape(document.Title)).Append("\n\n");
        sb.Append("Window ")
            .Append(w.FromDate)
            .Append(" → ")
            .Append(w.ToDate)
            .Append(" (")
            .Append(Escape(w.Since))
            .Append(") · store ")
            .Append(Code(w.Store))
            .Append('\n');
        foreach (var section in document.Sections)
        {
            sb.Append("\n## ").Append(Escape(section.Title)).Append('\n');
            foreach (var block in section.Blocks)
            {
                if (Block(block) is { } text)
                {
                    sb.Append('\n').Append(text);
                }
            }
        }
        return sb.ToString();
    }

    private static string? Block(ReportBlock block) =>
        block switch
        {
            Paragraph p => Spans(p.Spans) + "\n",
            Table t => Table(t),
            CodeBlock c => Fence(c.Text),
            Findings { Items.Count: 0 } => Findings.None + "\n",
            Findings f => string.Concat(
                f.Items.Select(i =>
                    $"- **{Escape(i.Rule)}**: {Escape(i.Text)} ({Escape(i.Evidence)})\n"
                )
            ),
            _ => null,
        };

    private static string Spans(IEnumerable<Span> spans) =>
        string.Concat(
            spans.Select(s =>
                s.Kind switch
                {
                    SpanKind.Code => Code(s.Text),
                    SpanKind.Strong => "**" + Escape(s.Text) + "**",
                    _ => Escape(s.Text),
                }
            )
        );

    private static string Table(Table t)
    {
        var sb = new StringBuilder();
        sb.Append("| ").AppendJoin(" | ", t.Columns.Select(c => Escape(c.Name))).Append(" |\n");
        sb.Append("| ")
            .AppendJoin(" | ", t.Columns.Select(c => c.Numeric ? "---:" : "---"))
            .Append(" |\n");
        foreach (var row in t.Rows)
        {
            sb.Append("| ").AppendJoin(" | ", row.Select(Escape)).Append(" |\n");
        }
        return sb.ToString();
    }

    /// <summary>Inline data: one line, every Markdown and HTML metacharacter escaped.</summary>
    internal static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '\n' or '\r')
            {
                sb.Append(' ');
                continue;
            }
            if (
                c
                is '\\'
                    or '`'
                    or '*'
                    or '_'
                    or '['
                    or ']'
                    or '<'
                    or '>'
                    or '|'
                    or '#'
                    or '!'
                    or '&'
            )
            {
                sb.Append('\\');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>A code span fenced by more backticks than the text holds in a row.</summary>
    private static string Code(string text)
    {
        var line = text.Replace('\n', ' ').Replace('\r', ' ');
        var fence = new string('`', LongestRun(line, '`') + 1);
        var pad = line.StartsWith('`') || line.EndsWith('`') ? " " : "";
        return fence + pad + line + pad + fence;
    }

    private static string Fence(string text)
    {
        var fence = new string('`', Math.Max(3, LongestRun(text, '`') + 1));
        var body = text.EndsWith('\n') ? text : text + "\n";
        return $"{fence}text\n{body}{fence}\n";
    }

    private static int LongestRun(string text, char c)
    {
        int best = 0,
            run = 0;
        foreach (var ch in text)
        {
            run = ch == c ? run + 1 : 0;
            best = Math.Max(best, run);
        }
        return best;
    }
}
