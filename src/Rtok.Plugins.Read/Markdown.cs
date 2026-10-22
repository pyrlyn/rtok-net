namespace Rtok.Plugins.Read;

/// <summary>The line scans behind a Markdown or <c>llms.txt</c> outline.</summary>
public static class Markdown
{
    /// <summary><c>#</c> headings with their first non-empty body line; fenced blocks are
    /// skipped.</summary>
    public static IReadOnlyList<MdHeading> Headings(string src)
    {
        var headings = new List<MdHeading>();
        var fenced = false;
        int? slot = null;
        var lines = TextLines.Split(src);
        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines[i].Trim();
            if (t.StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
            }
            else if (fenced) { }
            else if (t.StartsWith('#'))
            {
                var level = Math.Clamp(t.Length - t.TrimStart('#').Length, 1, 6);
                headings.Add(new MdHeading(i + 1, level, t, null));
                slot = headings.Count - 1;
            }
            else if (slot is { } j && t.Length > 0)
            {
                headings[j] = headings[j] with { Body = t };
                slot = null;
            }
        }

        return headings;
    }

    /// <summary>Each heading line, then its first body line indented by two spaces.</summary>
    public static string Digest(string src)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var h in Headings(src))
        {
            sb.Append(h.Text).Append('\n');
            if (h.Body is { } body)
            {
                sb.Append("  ").Append(body).Append('\n');
            }
        }

        return sb.ToString();
    }

    /// <summary><c>- [title](url)</c> and <c>* [title](url): note</c> lines; fenced blocks are
    /// skipped.</summary>
    public static IReadOnlyList<LlmsLink> LlmsLinks(string src)
    {
        var links = new List<LlmsLink>();
        var fenced = false;
        foreach (var line in TextLines.Split(src))
        {
            var t = line.Trim();
            if (t.StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
            }
            else if (!fenced && ParseLink(t) is { } link)
            {
                links.Add(link);
            }
        }

        return links;
    }

    /// <summary><c>llms.txt</c> and <c>llms-full.txt</c> are Markdown with a <c>.txt</c>
    /// name.</summary>
    public static bool IsLlmsTxt(string path) =>
        Path.GetFileName(path) is "llms.txt" or "llms-full.txt";

    internal static bool IsMarkdown(string path) => Outline.Extension(path) is "md" or "mdx";

    internal static string Render(IReadOnlyList<MdHeading> headings, string mode) =>
        mode is "signatures"
            ? string.Join('\n', headings.Select(h => h.Text))
            : string.Join(
                '\n',
                headings.Select(h =>
                {
                    var row = $"h{h.Level} {h.Text.TrimStart('#').Trim()} {h.Line}";
                    return h.Body is { } body ? $"{row}\n  {body}" : row;
                })
            );

    internal static string RenderLinks(IReadOnlyList<LlmsLink> links) =>
        string.Join(
            '\n',
            links.Select(l =>
                l.Description.Length == 0
                    ? $"link {l.Title} {l.Url}"
                    : $"link {l.Title} {l.Url} — {l.Description}"
            )
        );

    private static LlmsLink? ParseLink(string line)
    {
        if (
            !line.StartsWith("- ", StringComparison.Ordinal)
            && !line.StartsWith("* ", StringComparison.Ordinal)
        )
        {
            return null;
        }

        var rest = line[2..].Trim();
        if (!rest.StartsWith('['))
        {
            return null;
        }

        var mid = rest.IndexOf("](", StringComparison.Ordinal);
        if (mid < 0)
        {
            return null;
        }

        var title = rest[1..mid].Trim();
        var tail = rest[(mid + 2)..];
        var close = tail.IndexOf(')', StringComparison.Ordinal);
        if (title.Length == 0 || close < 0)
        {
            return null;
        }

        var url = tail[..close].Trim();
        if (url.Length == 0 || url.Any(char.IsWhiteSpace))
        {
            return null;
        }

        var note = tail[(close + 1)..].Trim();
        return new LlmsLink(title, url, note.StartsWith(':') ? note[1..].Trim() : "");
    }
}
