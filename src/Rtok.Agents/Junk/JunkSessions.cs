using System.Text.Json;

namespace Rtok.Agents.Junk;

/// <summary>
/// The <c>sessions</c> kind (D36): old sessions, class explicit (only <c>--kind sessions</c>
/// clears them), judged by time alone. Only Claude Code documents the whole session unit
/// (research.md §22.1): <c>projects/&lt;p&gt;/&lt;id&gt;.jsonl</c>, <c>&lt;id&gt;/</c> and
/// <c>file-history/&lt;id&gt;/</c>. The host's memory never matches a session.
/// </summary>
public static class JunkSessions
{
    /// <summary>A session written to in this window may be open in its agent, whatever the threshold says.</summary>
    public static readonly TimeSpan InUse = TimeSpan.FromMinutes(10);

    /// <summary>Claude Code names a session by a UUID.</summary>
    internal static bool IsSessionId(string s) =>
        s.Length == 36
        && s.Select((c, i) => i is 8 or 13 or 18 or 23 ? c == '-' : char.IsAsciiHexDigit(c))
            .All(ok => ok);

    /// <summary>Why a session with these parts stays, or null when it is old. One answer for all parts, so a session goes whole or not at all.</summary>
    internal static string? Stays(IEnumerable<string> parts, uint days, DateTime now)
    {
        DateTime? last = null;
        foreach (var p in parts)
        {
            if (JunkTree.Newest(p) is not (true, { } t))
            {
                return "last used unknown: not cleared";
            }
            last = last is { } l && l >= t ? l : t;
        }
        if (last is not { } at)
        {
            return null;
        }
        var age = now - at;
        if (age < TimeSpan.Zero)
        {
            return "future timestamp: counted as just touched";
        }
        if (age < InUse)
        {
            return "skipped: in use (modified in the last 10 minutes)";
        }
        return age <= TimeSpan.FromDays(days) ? $"touched within the {days}-day threshold" : null;
    }

    /// <summary>Claude Code's sessions, each part an item of its own with the session's answer. <paramref name="days"/> is <c>stale_session_days</c>.</summary>
    internal static List<JunkItem> SessionItems(JunkRoots roots, uint days, JunkContext cx)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(cx);
        var claude = roots.Resolve("{claude}");
        var items = new List<JunkItem>();
        foreach (
            var project in JunkTree
                .Children(Path.Combine(claude, "projects"))
                .Where(JunkTree.IsDirectory)
        )
        {
            foreach (var transcript in JunkTree.Children(project).Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileName(transcript);
                if (
                    !name.EndsWith(".jsonl", StringComparison.Ordinal)
                    || !JunkTree.IsFile(transcript)
                )
                {
                    continue;
                }
                var id = name[..^".jsonl".Length];
                if (!IsSessionId(id))
                {
                    continue;
                }
                List<string> parts = [transcript];
                parts.AddRange(
                    new[]
                    {
                        Path.Combine(project, id),
                        Path.Combine(claude, "file-history", id),
                    }.Where(JunkTree.IsDirectory)
                );
                var kept = Stays(parts, days, cx.Now);
                items.AddRange(
                    parts.Select(p =>
                        JunkCache.Make("sessions", p, JunkEvidence.Section22, kept, cx, "explicit")
                    )
                );
            }
        }
        return items;
    }

    /// <summary>What the list says about the threshold in use.</summary>
    public static string Note(uint days)
    {
        var note = $"old sessions: not touched for more than {days} days";
        return days == 0 ? $"{note} (threshold 0: every closed session is junk)" : note;
    }

    /// <summary>
    /// What the host prunes by itself, beside rtok's threshold: Claude Code's
    /// <c>cleanupPeriodDays</c> (default 30) and Gemini CLI's <c>general.sessionRetention.maxAge</c>
    /// (default 30d), from the host's settings.json.
    /// </summary>
    public static string? Retention(JunkRoots roots, string host)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var (file, key, fallback) = host switch
        {
            "claude" => (roots.Resolve("{claude}/settings.json"), "cleanupPeriodDays", "30"),
            "gemini" => (
                roots.Resolve("{home}/.gemini/settings.json"),
                "general.sessionRetention.maxAge",
                "30d"
            ),
            _ => (null, "", ""),
        };
        if (file is null)
        {
            return null;
        }
        string? set = null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var at = doc.RootElement;
            var found = true;
            foreach (var k in key.Split('.'))
            {
                if (at.ValueKind != JsonValueKind.Object || !at.TryGetProperty(k, out at))
                {
                    found = false;
                    break;
                }
            }
            if (found)
            {
                set = at.ValueKind == JsonValueKind.String ? at.GetString() : at.GetRawText();
            }
        }
        // A missing or unreadable file is the host's default, not an error.
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { }
        var text = set is null ? $"{key} = {fallback} (default)" : $"{key} = {set}";
        return $"host retention: {text}";
    }
}
