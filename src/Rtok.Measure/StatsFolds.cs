using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Rtok.Agents.Usage;
using static Rtok.Measure.StatsReport;

namespace Rtok.Measure;

/// <summary>The per-session folds behind the report's call rows, each over one parsed transcript.</summary>
internal static partial class StatsFolds
{
    /// <summary>How many tool calls after a deny count as "what the agent did next".</summary>
    public const int DenyFollowCalls = 3;

    /// <summary>Path-named write signals; words are whole shell tokens, so <c>rm</c> never matches inside <c>warm</c>.</summary>
    private static readonly string[] WriteSymbols = [">", "sed -i", "perl -i", "perl -pi", "tee "];
    private static readonly string[] WriteWords = ["mv", "cp", "rm", "touch", "patch"];

    /// <summary>Writers that can rewrite any tracked file without naming it.</summary>
    private static readonly string[] BlanketWriters =
    [
        "git checkout",
        "git restore",
        "git stash",
        "git reset",
        "git rebase",
        "git merge",
        "git pull",
        "git apply",
        "git cherry-pick",
        "cargo fmt",
        "rustfmt",
        "oxfmt",
        "prettier --write",
    ];

    private static readonly char[] WordBreaks = [' ', '\t', '\n', '\r', '\f', '\v', '&', '|', ';'];

    /// <summary>Every tool input counts toward the denominator; Edit and MultiEdit add what they quote.</summary>
    public static void Edits(EditRow row, Transcript.ToolUse u)
    {
        row.ToolInputBytes += CompactLength(u.Input);
        IEnumerable<JsonElement> edits = u.Name switch
        {
            "Edit" => [u.Input],
            "MultiEdit" when Json.At(u.Input, "edits") is { ValueKind: JsonValueKind.Array } a =>
                a.EnumerateArray(),
            _ => [],
        };
        foreach (var e in edits)
        {
            row.Calls++;
            row.OldBytes += Transcript.Utf8(Json.Text(e, "old_string") ?? "");
            row.NewBytes += Transcript.Utf8(Json.Text(e, "new_string") ?? "");
        }
    }

    /// <summary>A Read of a path read before, with an edit of it in between, is a changed re-read.</summary>
    public static void ReadDelta(ReadDeltaRow row, Transcript t)
    {
        var sizes = Sizes(t);
        var paths = new List<(string Path, bool Seen, bool Dirty)>();
        foreach (var u in t.ToolUses)
        {
            if (Calls.ToolPath(u.Input) is not { } path)
            {
                continue;
            }
            var i = paths.FindIndex(p => Calls.SamePath(p.Path, path));
            switch (u.Name)
            {
                case "Read":
                    var bytes = sizes.GetValueOrDefault(u.Id);
                    row.ReadBytes += bytes;
                    if (i < 0)
                    {
                        paths.Add((path, true, false));
                        break;
                    }
                    if (paths[i] is { Seen: true, Dirty: true })
                    {
                        row.Calls++;
                        row.Bytes += bytes;
                    }
                    paths[i] = (paths[i].Path, true, false);
                    break;
                case "Edit" or "Write" or "MultiEdit":
                    if (i < 0)
                    {
                        paths.Add((path, false, true));
                    }
                    else
                    {
                        paths[i] = (paths[i].Path, paths[i].Seen, true);
                    }
                    break;
            }
        }
    }

    /// <summary>Whole, large Reads of outlined files; those an edit follows within the window needed the body.</summary>
    public static void ReadWhole(ReadWholeRow row, Transcript t, Replay rp, StatsSignals signals)
    {
        var sizes = Sizes(t);
        row.ResultBytes += sizes.Values.Sum();
        for (var i = 0; i < t.ToolUses.Count; i++)
        {
            var u = t.ToolUses[i];
            if (u.Name is not "Read")
            {
                continue;
            }
            var bytes = sizes.GetValueOrDefault(u.Id);
            row.ReadBytes += bytes;
            var ranged =
                Json.At(u.Input, "offset") is not null || Json.At(u.Input, "limit") is not null;
            if (
                Calls.ToolPath(u.Input) is not { } path
                || ranged
                || bytes < Math.Max(rp.ReadWholeMin, 1)
                || !signals.HasOutline(path)
            )
            {
                continue;
            }
            row.Calls++;
            row.Bytes += bytes;
            var edited = t
                .ToolUses.Skip(i + 1)
                .TakeWhile(e => e.Turn <= u.Turn + rp.EditWindowTurns)
                .Any(e =>
                    e.Name is "Edit" or "Write" or "MultiEdit"
                    && Calls.ToolPath(e.Input) is { } p
                    && Calls.SamePath(p, path)
                );
            if (edited)
            {
                row.Edited++;
                row.EditedBytes += bytes;
            }
        }
    }

    /// <summary>Denied Reads with their reasons' size, classed by what the next calls did.</summary>
    public static void ReadDeny(ReadDenyRow row, Transcript t, StatsSignals signals)
    {
        var results = Results(t);
        for (var i = 0; i < t.ToolUses.Count; i++)
        {
            var u = t.ToolUses[i];
            if (
                u.Name is not "Read"
                || !results.TryGetValue(u.Id, out var text)
                || !signals.IsReadDeny(text)
            )
            {
                continue;
            }
            row.Calls++;
            row.Tokens += Calls.EstTokens(Transcript.Utf8(text));
            var window = t.ToolUses.Skip(i + 1).Take(DenyFollowCalls).ToList();
            if (
                window
                    .TakeWhile(n => n.Name is not "Read" && McpGroup(n.Name) is not "rtok")
                    .Any(n => n.Name is "ToolSearch")
            )
            {
                row.ThenToolSearch++;
            }
            if (Calls.ToolPath(u.Input) is not { } path)
            {
                continue;
            }
            var next = window.Find(n =>
                (n.Name is "Read" || IsRtokMcpRead(n.Name))
                && Calls.ToolPath(n.Input) is { } p
                && Calls.SamePath(p, path)
            );
            if (next is { Name: "Read" })
            {
                row.ThenNative++;
            }
            else if (next is not null)
            {
                row.ThenMcp++;
            }
        }
    }

    /// <summary>A result whose SHA-256 matches an earlier one in the session is a repeat.</summary>
    public static void Repeat(RepeatRow row, Transcript t)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in t.ToolResults)
        {
            var bytes = Encoding.UTF8.GetBytes(r.Content);
            row.ResultBytes += bytes.Length;
            if (!seen.Add(Convert.ToHexStringLower(SHA256.HashData(bytes))))
            {
                row.Calls++;
                row.Bytes += bytes.Length;
            }
        }
    }

    /// <summary>
    /// A result naming an archive id makes the id shown; a later <c>rtok expand</c> Bash call or
    /// MCP <c>expand</c> of a shown id is a re-expand. Ids key by their 12-char prefix, so a full
    /// id matches a prefix the trailer showed.
    /// </summary>
    public static void ExpandAfter(ExpandAfterRow row, Transcript t)
    {
        var results = Results(t);
        var shown = new Dictionary<string, long?>(StringComparer.Ordinal);
        static string Key(string id) => id[..Math.Min(id.Length, 12)];
        foreach (var u in t.ToolUses)
        {
            var content = results.GetValueOrDefault(u.Id) ?? "";
            var asked =
                u.Name.EndsWith("expand", StringComparison.Ordinal) ? Json.Text(u.Input, "id")
                : u.Name is "Bash"
                    ? Ids(Called(), Json.Text(u.Input, "command") ?? "").FirstOrDefault()
                : null;
            if (asked is not null && shown.TryGetValue(Key(asked), out var seen))
            {
                row.Calls++;
                row.Bytes += Transcript.Utf8(content);
                row.ShownBytes += seen ?? 0;
                shown[Key(asked)] = null;
                continue;
            }
            foreach (var id in Ids(Named(), content))
            {
                shown.TryAdd(Key(id), Transcript.Utf8(content));
            }
        }
    }

    /// <summary>Classifies each repeat native Read; <c>subagent</c> comes from <see cref="Subagents"/>.</summary>
    public static void RepeatReads(RepeatReadsRow row, Transcript t, StatsSignals signals)
    {
        var results = Results(t);
        var sawRtok = results.Values.Any(c => HasRtokMarker(c, signals));
        var paths = new List<(string Path, long? Offset, long? Limit, bool Touched)>();
        foreach (var u in t.ToolUses)
        {
            switch (u.Name)
            {
                case "Edit" or "Write" or "MultiEdit":
                    if (
                        Calls.ToolPath(u.Input) is { } edited
                        && paths.FindIndex(p => Calls.SamePath(p.Path, edited)) is var e and >= 0
                    )
                    {
                        paths[e] = paths[e] with { Touched = true };
                    }
                    break;
                case "Bash":
                    var cmd = Json.Text(u.Input, "command") ?? "";
                    for (var i = 0; i < paths.Count; i++)
                    {
                        if (BashTouches(cmd, paths[i].Path))
                        {
                            paths[i] = paths[i] with { Touched = true };
                        }
                    }
                    break;
                case "Read" when Calls.ToolPath(u.Input) is { } path:
                    var content = results.GetValueOrDefault(u.Id) ?? "";
                    var (offset, limit) = (Unsigned(u.Input, "offset"), Unsigned(u.Input, "limit"));
                    var at = paths.FindIndex(p => Calls.SamePath(p.Path, path));
                    if (at < 0)
                    {
                        paths.Add((path, offset, limit, false));
                        break;
                    }
                    var p = paths[at];
                    var cls =
                        HasRtokMarker(content, signals) ? row.Fired
                        : p.Touched ? row.Changed
                        : p.Offset != offset || p.Limit != limit ? row.Ranged
                        : sawRtok ? row.Declined
                        : row.HookAbsent;
                    cls.Calls++;
                    cls.Bytes += Transcript.Utf8(content);
                    paths[at] = (p.Path, offset, limit, false);
                    break;
            }
        }
    }

    /// <summary>
    /// What <c>ctt</c> becomes when the <c>archive</c> plugin swaps the result for its pointer
    /// after <c>keep_turns</c>: full size while young, head and tail lines afterwards.
    /// </summary>
    public static long ReplayCtt(
        string content,
        long tokens,
        long remain,
        Replay rp,
        StatsSignals signals
    )
    {
        if (tokens < rp.MinTokens || remain <= rp.KeepTurns)
        {
            return SizeRows.SatMul(tokens, remain);
        }
        var lines = Json.Lines(content).ToList();
        // With no more lines than head + tail the pointer shows the body once, never twice.
        var n = lines.Count;
        var (head, tail) =
            n <= (long)rp.HeadLines + rp.TailLines ? (n, 0) : (rp.HeadLines, rp.TailLines);
        var kept = lines
            .Take(head)
            .Concat(lines.Skip(n - tail))
            .Sum(l => signals.ShownLength(l) + 1);
        var pointer = Calls.EstTokens(kept + 64); // + the `[archived …]` line itself
        return SizeRows.Sat(
            SizeRows.SatMul(tokens, rp.KeepTurns),
            SizeRows.SatMul(pointer, remain - rp.KeepTurns)
        );
    }

    /// <summary>
    /// A Bash command that could have changed <paramref name="path"/>: a blanket writer, or a
    /// write signal in a command that names the path or its file name.
    /// </summary>
    public static bool BashTouches(string cmd, string path)
    {
        if (BlanketWriters.Any(w => cmd.Contains(w, StringComparison.Ordinal)))
        {
            return true;
        }
        var file = Path.GetFileName(path);
        var named =
            cmd.Contains(path, StringComparison.Ordinal)
            || (file.Length > 0 && cmd.Contains(file, StringComparison.Ordinal));
        return named
            && (
                WriteSymbols.Any(w => cmd.Contains(w, StringComparison.Ordinal))
                || cmd.Split(WordBreaks).Any(t => WriteWords.Contains(t, StringComparer.Ordinal))
            );
    }

    /// <summary>
    /// A native Read result carrying rtok's trace: a deny reason, a dedup answer, an archive
    /// pointer or a trailer.
    /// </summary>
    public static bool HasRtokMarker(string content, StatsSignals signals) =>
        signals.IsReadDeny(content)
        || signals.IsDedup(content)
        || content.StartsWith("[archived ", StringComparison.Ordinal)
        || content.Contains("[rtok ", StringComparison.Ordinal);

    /// <summary>The server of an MCP tool name (<c>mcp__&lt;server&gt;__&lt;tool&gt;</c>); null for a native tool.</summary>
    public static string? McpGroup(string name)
    {
        if (!name.StartsWith("mcp__", StringComparison.Ordinal))
        {
            return null;
        }
        var rest = name[5..];
        var end = rest.IndexOf("__", StringComparison.Ordinal);
        return end < 0 ? rest : rest[..end];
    }

    /// <summary>The input as compact JSON, in UTF-8 bytes; an absent input is <c>null</c>.</summary>
    public static long CompactLength(JsonElement input)
    {
        if (input.ValueKind == JsonValueKind.Undefined)
        {
            return 4;
        }
        var buffer = new ArrayBufferWriter<byte>();
        using (
            var w = new Utf8JsonWriter(
                buffer,
                new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }
            )
        )
        {
            input.WriteTo(w);
        }
        return buffer.WrittenCount;
    }

    private static bool IsRtokMcpRead(string name) =>
        McpGroup(name) is "rtok" && name.EndsWith("__read", StringComparison.Ordinal);

    private static long? Unsigned(JsonElement input, string name) =>
        Json.At(input, name) is { ValueKind: JsonValueKind.Number } n && n.TryGetUInt64(out var v)
            ? (long)Math.Min(v, long.MaxValue)
            : null;

    /// <summary>Each result's text by its call id; a later result for the same id wins.</summary>
    private static Dictionary<string, string> Results(Transcript t)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in t.ToolResults)
        {
            map[r.ToolUseId] = r.Content;
        }
        return map;
    }

    private static Dictionary<string, long> Sizes(Transcript t) =>
        Results(t)
            .ToDictionary(kv => kv.Key, kv => Transcript.Utf8(kv.Value), StringComparer.Ordinal);

    /// <summary>The matches' <c>id</c> groups; a match that times out ends the list, never the report.</summary>
    private static List<string> Ids(Regex re, string text)
    {
        var ids = new List<string>();
        try
        {
            for (var m = re.Match(text); m.Success; m = m.NextMatch())
            {
                ids.Add(m.Groups["id"].Value);
            }
        }
        catch (RegexMatchTimeoutException) { }
        return ids;
    }

    [GeneratedRegex(
        @"(?:expand:? (?:rtok expand )?|\[rtok )(?<id>[0-9a-f]{12,64})\b",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        1000
    )]
    private static partial Regex Named();

    [GeneratedRegex(
        "rtok expand (?<id>[0-9a-f]{12,64})",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        1000
    )]
    private static partial Regex Called();
}
