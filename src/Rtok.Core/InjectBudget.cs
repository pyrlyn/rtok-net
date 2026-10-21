using System.Text;
using System.Text.Json;
using Rtok.Plugin.Sdk;

namespace Rtok.Core;

/// <summary>
/// The context budget (<c>plugins.inject.budget_tokens</c>): what the hook adds to an agent's
/// context never goes over it, and whatever is cut is named by a <c>dropped:</c> marker.
/// </summary>
public static class InjectBudget
{
    /// <summary>The budget when the config sets none.</summary>
    public const int DefaultTokens = 800;

    // An archive id is 64 hex digits whatever the bytes, so a placeholder of that length
    // prices the marker before anything is archived.
    private static readonly string IdPlaceholder = new('0', 64);

    /// <summary><c>plugins.inject.budget_tokens</c>, or <see cref="DefaultTokens"/>.</summary>
    public static int Tokens(IHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return
            host.ConfigJson("plugins.inject") is { ValueKind: JsonValueKind.Object } section
            && section.TryGetProperty("budget_tokens", out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var tokens)
            && tokens >= 0
            ? tokens
            : DefaultTokens;
    }

    /// <summary>
    /// The injections offered for one event, highest priority first (ties by plugin id), each
    /// kept whole while it fits; the one that crosses the budget keeps a prefix, and the rest
    /// leave only <c>dropped:&lt;plugin&gt;:&lt;tokens&gt;</c> lines. Records one
    /// <c>inject</c> Measurement.
    /// </summary>
    public static string Apply(IHost host, IEnumerable<Injection> offered, int budget)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(offered);
        var parts = new List<string>();
        var dropped = new List<string>();
        var used = 0;
        var before = 0;
        long beforeBytes = 0;
        foreach (
            var injection in offered
                .OrderByDescending(i => i.Priority)
                .ThenBy(i => i.Plugin, StringComparer.Ordinal)
        )
        {
            var tokens = host.Estimate(injection.Text, TextClass.Prose);
            before += tokens;
            beforeBytes += Encoding.UTF8.GetByteCount(injection.Text);
            var marker = $"dropped:{injection.Plugin}:{tokens}";
            if (used >= budget)
            {
                dropped.Add(marker);
                continue;
            }

            if (tokens <= budget)
            {
                used += tokens;
                parts.Add(injection.Text);
                continue;
            }

            var prefix = CrossingPrefix(host, injection, marker, budget - used, parts.Count > 0);
            used = budget;
            if (prefix.Length > 0)
            {
                parts.Add(prefix);
            }

            dropped.Add(marker);
        }

        var text = string.Join('\n', parts);
        if (dropped.Count > 0)
        {
            text = (text.Length > 0 ? text + "\n" : "") + string.Join('\n', dropped);
        }

        host.Record(
            new Measurement
            {
                Plugin = "inject",
                Kind = "inject",
                BeforeBytes = beforeBytes,
                AfterBytes = Encoding.UTF8.GetByteCount(text),
                EstBefore = before,
                EstAfter = host.Estimate(text, TextClass.Prose),
                RefId = dropped.Count > 0 ? dropped[0] : null,
            }
        );
        return text;
    }

    // The one injection that crosses the budget keeps the prefix that fits beside its marker.
    private static string CrossingPrefix(
        IHost host,
        Injection injection,
        string marker,
        int room,
        bool afterOthers
    )
    {
        room -= host.Estimate("\n" + marker, TextClass.Prose);
        if (afterOthers)
        {
            room--;
        }

        return Fit(host, injection.Text, TextClass.Prose, Math.Max(0, room));
    }

    /// <summary>
    /// PostToolUse context fitted to <paramref name="budget"/>: whole lines while they fit,
    /// the rest archived once and named by
    /// <c>dropped:post_tool:&lt;tokens&gt; · expand: rtok expand &lt;id&gt;</c>. The marker
    /// always survives: kept lines are given back until it fits.
    /// </summary>
    public static string Cap(ICapabilities host, string text, int budget)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(text);
        if (host.Estimate(text, TextClass.Prose) <= budget)
        {
            return text;
        }

        var lines = Lines(text);
        var keep = 0;
        while (
            keep < lines.Count && host.Estimate(Join(lines, 0, keep + 1), TextClass.Prose) <= budget
        )
        {
            keep++;
        }

        if (keep == lines.Count)
        {
            return Join(lines, 0, keep);
        }

        while (keep > 0)
        {
            var priced =
                Join(lines, 0, keep) + "\n" + Marker(host, Join(lines, keep), IdPlaceholder);
            if (host.Estimate(priced, TextClass.Prose) <= budget)
            {
                break;
            }

            keep--;
        }

        var rest = Join(lines, keep);
        var marker = Marker(host, rest, Archive(host, rest));
        if (keep > 0)
        {
            return Join(lines, 0, keep) + "\n" + marker;
        }

        // Estimates round up per part, so a prefix that fits the room left after the marker
        // keeps the whole under budget. The archive holds the whole line, prefix included.
        var room = Math.Max(0, budget - host.Estimate("\n" + marker, TextClass.Prose));
        var prefix = Fit(host, lines[0], TextClass.Prose, room);
        if (prefix.Length > 0)
        {
            return prefix + "\n" + marker;
        }

        return host.Estimate(marker, TextClass.Prose) <= budget ? marker : "";
    }

    /// <summary>The longest prefix of <paramref name="text"/>, in whole Unicode scalars, that
    /// estimates within <paramref name="budget"/>.</summary>
    public static string Fit(IHost host, string text, TextClass textClass, int budget)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(text);
        var estimate = host.Estimate(text, textClass);
        if (estimate <= budget)
        {
            return text;
        }

        var scalars = 0;
        foreach (var _ in text.EnumerateRunes())
        {
            scalars++;
        }

        var keep = (int)((long)scalars * Math.Max(0, budget) / estimate);
        var length = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (keep-- == 0)
            {
                break;
            }

            length += rune.Utf16SequenceLength;
        }

        var prefix = text[..length];
        while (prefix.Length > 0 && host.Estimate(prefix, textClass) > budget)
        {
            var last = char.IsLowSurrogate(prefix[^1]) && prefix.Length > 1 ? 2 : 1;
            prefix = prefix[..^last];
        }

        return prefix;
    }

    private static string Marker(IHost host, string dropped, string id)
    {
        var tokens = host.Estimate(dropped, TextClass.Prose);
        return id.Length == 0
            ? $"dropped:post_tool:{tokens}"
            : $"dropped:post_tool:{tokens} · expand: rtok expand {id}";
    }

    // Fail open: a store that cannot archive still gets the marker, without an id.
    private static string Archive(IArchive archive, string dropped)
    {
        try
        {
            return archive.PutArchive(Encoding.UTF8.GetBytes(dropped));
        }
#pragma warning disable CA1031 // The hook never fails over a dropped-context archive.
        catch (Exception)
#pragma warning restore CA1031
        {
            return "";
        }
    }

    // Lines as Rust's str::lines splits them: on '\n', one trailing '\r' dropped from each,
    // no empty last line after a final '\n'.
    private static List<string> Lines(string text)
    {
        var lines = text.Split('\n').Select(l => l.EndsWith('\r') ? l[..^1] : l).ToList();
        if (text.EndsWith('\n'))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private static string Join(List<string> lines, int start, int count) =>
        string.Join('\n', lines.Skip(start).Take(count));

    private static string Join(List<string> lines, int start) =>
        Join(lines, start, lines.Count - start);
}
