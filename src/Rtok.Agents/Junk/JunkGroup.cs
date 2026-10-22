using System.Globalization;
using System.Text;

namespace Rtok.Agents.Junk;

/// <summary>The rows of one kind under one agent, cut to what the text prints.</summary>
public sealed class JunkGroup
{
    internal JunkGroup(string agent, string kind, List<JunkRow> shown, List<JunkRow> hidden)
    {
        Agent = agent;
        Kind = kind;
        Shown = shown;
        More = hidden.Count;
        MoreBytes = hidden.Sum(r => r.SizeBytes);
    }

    /// <summary>The agent.</summary>
    public string Agent { get; }

    /// <summary>The kind.</summary>
    public string Kind { get; }

    internal List<JunkRow> Shown { get; }

    internal int More { get; }

    internal long MoreBytes { get; }

    /// <summary>The group's lines at <paramref name="indent"/>.</summary>
    public string Lines(JunkView view, string indent)
    {
        ArgumentNullException.ThrowIfNull(view);
        var sb = new StringBuilder();
        if (view.Shown == 0)
        {
            return "";
        }
        foreach (var r in Shown)
        {
            var path = view.Tilde(r.Path);
            if (view.Links)
            {
                path = JunkView.Link(r.Path, path);
            }
            sb.Append(indent)
                .Append(path)
                .Append("  ")
                .Append(view.Size(r.SizeBytes))
                .Append("  last used ")
                .Append(view.When(r.LastUsed))
                .Append("  ")
                .Append(r.Reason);
            if (r.SkipReason is { } why)
            {
                sb.Append("  (not cleared: ").Append(why).Append(')');
            }
            else if (r.Result.Length > 0)
            {
                sb.Append("  (").Append(r.Result).Append(')');
            }
            sb.Append('\n');
        }
        if (More > 0)
        {
            sb.Append(indent)
                .Append(CultureInfo.InvariantCulture, $"+{More} more (")
                .Append(view.Size(MoreBytes))
                .Append(")\n");
        }
        return sb.ToString();
    }

    /// <summary>Order the rows as the view says: size (largest first), last-used (longest unused first, unknown last) or path.</summary>
    internal static List<JunkRow> Order(IEnumerable<JunkRow> rows, JunkView view)
    {
        if (view.Sort is "last-used")
        {
            var all = rows.ToList();
            all.ForEach(r => r.Stamp(view));
            return
            [
                .. all.OrderBy(r => r.LastUsed is null)
                    .ThenBy(r => r.LastUsed)
                    .ThenBy(r => r.Path, StringComparer.Ordinal),
            ];
        }
        return view.Sort is "path"
            ? [.. rows.OrderBy(r => r.Path, StringComparer.Ordinal)]
            :
            [
                .. rows.OrderByDescending(r => r.SizeBytes)
                    .ThenBy(r => r.Path, StringComparer.Ordinal),
            ];
    }

    /// <summary>The text groups, in the order their first row came; only counted rows at least <see cref="JunkView.MinSize"/>.</summary>
    internal static List<JunkGroup> Of(IEnumerable<JunkRow> rows, JunkView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return
        [
            .. rows.Where(r => r.Counted && r.SizeBytes >= view.MinSize)
                .GroupBy(r => (r.Agent, r.Kind))
                .Select(g =>
                {
                    var ordered = Order(g, view);
                    var cut = Math.Min(ordered.Count, view.Shown);
                    var shown = ordered[..cut];
                    shown.ForEach(r => r.Stamp(view));
                    return new JunkGroup(g.Key.Agent, g.Key.Kind, shown, ordered[cut..]);
                }),
        ];
    }
}
