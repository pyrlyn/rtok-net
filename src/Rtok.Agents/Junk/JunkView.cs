using System.Globalization;
using Pyrlyn.ChangePreview;

namespace Rtok.Agents.Junk;

/// <summary>How a breakdown prints: one run's flags.</summary>
public sealed record JunkView
{
    /// <summary>How many items a kind shows before "+N more" (the 10 largest).</summary>
    public const int DefaultShown = 10;

    /// <summary>Items per kind; 0 prints totals only, <see cref="int.MaxValue"/> all.</summary>
    public int Shown { get; init; } = DefaultShown;

    /// <summary><c>size</c>, <c>last-used</c> or <c>path</c>.</summary>
    public string Sort { get; init; } = "size";

    /// <summary>Only items at least this big.</summary>
    public long MinSize { get; init; }

    /// <summary>Raw byte counts instead of KB/MB/GB.</summary>
    public bool Exact { get; init; }

    /// <summary>OSC 8 links around paths; only for a terminal.</summary>
    public bool Links { get; init; }

    /// <summary><c>stale_session_days</c>, named in a session's reason.</summary>
    public uint Days { get; init; } = 30;

    /// <summary>The clock.</summary>
    public DateTime Now { get; init; }

    /// <summary>Paths under it print with <c>~</c>.</summary>
    public string? Home { get; init; }

    /// <summary>The zone dates print in.</summary>
    public TimeZoneInfo Zone { get; init; } = TimeZoneInfo.Local;

    /// <summary>A size as the view prints it.</summary>
    public string Size(long n) =>
        Exact ? n.ToString(CultureInfo.InvariantCulture) : PreviewText.HumanBytes(n);

    /// <summary><c>--items</c>: a count or <c>all</c>; null when neither.</summary>
    public static int? ParseShown(string s) =>
        s is "all" ? int.MaxValue
        : int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n
        : null;

    /// <summary><c>--min-size</c>: bytes, or a number with K, M or G (1024 based, an optional B); null when neither.</summary>
    public static long? ParseSize(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var t = s.Trim().ToUpperInvariant();
        if (t.EndsWith('B'))
        {
            t = t[..^1];
        }
        var shift = t.Length == 0 ? 0 : "KMG".IndexOf(t[^1], StringComparison.Ordinal) + 1;
        if (shift > 0)
        {
            t = t[..^1];
        }
        return
            long.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            && n <= long.MaxValue >> (10 * shift)
            ? n << (10 * shift)
            : null;
    }

    /// <summary><paramref name="text"/> as an OSC 8 link to <paramref name="path"/>; a relative path stays plain.</summary>
    public static string Link(string path, string text) =>
        Path.IsPathFullyQualified(path)
            ? $"\u001b]8;;{new Uri(path).AbsoluteUri}\u001b\\{text}\u001b]8;;\u001b\\"
            : text;

    internal string Tilde(string path)
    {
        if (Home is null || !JunkTree.Under(path, Home))
        {
            return path;
        }
        var rest = path[Home.TrimEnd('/', '\\').Length..].TrimStart('/', '\\');
        return rest.Length == 0 ? "~" : "~/" + rest.Replace('\\', '/');
    }

    /// <summary><c>3 weeks ago (2026-09-09 14:02)</c>, in <see cref="Zone"/>.</summary>
    internal string When(DateTime? last)
    {
        if (last is not { } t)
        {
            return "unknown";
        }
        var age = (long)Math.Max(0, (Now - t).TotalSeconds);
        static string Unit(long n, string name) =>
            string.Create(CultureInfo.InvariantCulture, $"{n} {name}{(n == 1 ? "" : "s")} ago");
        var rel = age switch
        {
            < 60 => "just now",
            < 3_600 => Unit(age / 60, "minute"),
            < 172_800 => Unit(age / 3_600, "hour"),
            < 1_209_600 => Unit(age / 86_400, "day"),
            < 5_184_000 => Unit(age / 604_800, "week"),
            _ => Unit(age / 2_592_000, "month"),
        };
        var abs = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(t, DateTimeKind.Utc), Zone);
        return $"{rel} ({abs.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)})";
    }

    /// <summary>Why an item is junk, in the words of the catalog.</summary>
    internal string Reason(string kind, string evidence, DateTime? last)
    {
        var days = last is { } t ? (long)Math.Max(0, (Now - t).TotalDays) : (long?)null;
        var b = (kind, days) switch
        {
            ("sessions", { } d) => string.Create(
                CultureInfo.InvariantCulture,
                $"not touched for {d} days (threshold {Days})"
            ),
            ("crash-dumps", { } d) => string.Create(
                CultureInfo.InvariantCulture,
                $"crash dump {d} days old"
            ),
            _ => $"{kind} ({evidence})",
        };
        return last > Now ? $"{b}, future timestamp" : b;
    }
}
