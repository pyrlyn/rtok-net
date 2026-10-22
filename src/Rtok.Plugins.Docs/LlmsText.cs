using System.Text;
using System.Text.RegularExpressions;
using Rtok.Store;

namespace Rtok.Plugins.Docs;

/// <summary>
/// <c>llms.txt</c> text: split into items on heading lines (rtok's rule), and its markdown links
/// that name a local file or directory.
/// </summary>
public static partial class LlmsText
{
    /// <summary>
    /// One item per heading section of <paramref name="body"/>: a line starting with <c>#</c>
    /// opens the next item, titled by the line without its <c>#</c>s; text before the first
    /// heading is titled <paramref name="title"/>. Sections with no text are dropped.
    /// </summary>
    public static IReadOnlyList<DocEntry> Split(
        string body,
        string kind = "llms",
        string title = "llms"
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        var items = new List<DocEntry>();
        var heading = title;
        var buf = new StringBuilder();
        foreach (var raw in body.Split('\n'))
        {
            var line = raw.EndsWith('\r') ? raw[..^1] : raw;
            if (line.StartsWith('#'))
            {
                Flush(items, heading, kind, buf);
                heading = line.TrimStart('#').Trim();
                if (heading.Length == 0)
                {
                    heading = title;
                }
                buf.Clear();
            }
            else
            {
                buf.Append(line).Append('\n');
            }
        }
        Flush(items, heading, kind, buf);
        return items;
    }

    /// <summary>
    /// The targets of <paramref name="body"/>'s markdown links (<c>[text](target)</c>) that are
    /// relative paths: no scheme, not an anchor, not absolute. A fragment or query is cut off.
    /// Each target is listed once, in order of first appearance.
    /// </summary>
    public static IReadOnlyList<string> LocalLinks(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var links = new List<string>();
        foreach (Match m in Link().Matches(body))
        {
            var target = m.Groups["target"].Value;
            var cut = target.IndexOfAny(['#', '?']);
            if (cut >= 0)
            {
                target = target[..cut];
            }
            if (
                target.Length == 0
                || target.Contains(':', StringComparison.Ordinal)
                || target.StartsWith('/')
                || target.StartsWith('\\')
                || links.Contains(target, StringComparer.Ordinal)
            )
            {
                continue;
            }
            links.Add(target);
        }
        return links;
    }

    private static void Flush(List<DocEntry> items, string heading, string kind, StringBuilder buf)
    {
        var text = buf.ToString().Trim();
        if (text.Length > 0)
        {
            items.Add(new DocEntry(heading, kind, text));
        }
    }

    [GeneratedRegex(
        @"\[[^\]\n]*\]\(\s*<?(?<target>[^)\s>]+)>?(?:\s+""[^""\n]*"")?\s*\)",
        RegexOptions.ExplicitCapture,
        1000
    )]
    private static partial Regex Link();
}
