using System.Text.Json;

namespace Rtok.Agents.Usage;

/// <summary>
/// Gemini CLI (<c>~/.gemini/tmp/&lt;project&gt;/chats/session-*.jsonl</c>): an append-only log of
/// chat messages, patches and rewinds. A <c>gemini</c> message carries <c>tokens</c>
/// (<c>input</c> includes <c>cached</c>; <c>thoughts</c> and <c>tool</c> are counted apart) and its
/// <c>model</c>. An older release wrote one <c>session-*.json</c> document with a <c>messages</c>
/// array, migrated only when opened again, so both are read. A message is keyed by its id and the
/// last record wins, as the CLI's own loader does; a rewound message was still billed.
/// </summary>
internal static class GeminiLogs
{
    public static HostLog Read(IEnumerable<string> dirs, long since)
    {
        var cutoff = LogSlice.Cutoff(since);
        var list = dirs.ToList();
        var files = LogFiles
            .Find(list, cutoff, "jsonl")
            .Concat(LogFiles.Find(list, cutoff, "json"))
            .Where(IsChat)
            .Order(StringComparer.Ordinal);
        var slices = new List<UsageSlice>();
        string? unreadable = null;
        foreach (var path in files)
        {
            if (LogText.Read(path) is { } text && Session(path, text, since) is { } rows)
            {
                slices.AddRange(rows);
            }
            else
            {
                unreadable ??= path;
            }
        }
        return new HostLog(slices, unreadable);
    }

    /// <summary>A recorded chat, not the project's <c>logs.json</c> or a checkpoint: <c>session-*</c> under <c>chats/</c>.</summary>
    private static bool IsChat(string path) =>
        Path.GetFileName(path).StartsWith("session-", StringComparison.Ordinal)
        && path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains("chats");

    /// <summary>The requests of one chat file; null when no line of it was JSON.</summary>
    internal static List<UsageSlice>? Session(string path, string text, long since)
    {
        var id = Path.GetFileNameWithoutExtension(path);
        var messages = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
        var parsed = 0;
        void Take(JsonElement v)
        {
            parsed++;
            id = Json.Text(v, "sessionId") ?? id;
            var listed = Json.At(v, "messages") is { ValueKind: JsonValueKind.Array } a
                ? a.EnumerateArray().ToList()
                : [];
            foreach (var m in listed.Append(v))
            {
                if (Json.Text(m, "id") is { } key)
                {
                    messages[key] = m;
                }
            }
        }
        IEnumerable<string> documents = path.EndsWith(".json", StringComparison.Ordinal)
            ? [text]
            : Json.Lines(text);
        foreach (var doc in documents)
        {
            if (Json.Parse(doc) is { } v)
            {
                Take(v);
            }
        }
        if (parsed == 0 && !string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        return [.. messages.Values.Select(m => Request(id, m, since)).OfType<UsageSlice>()];
    }

    private static UsageSlice? Request(string session, JsonElement m, long since)
    {
        if (Json.Text(m, "type") is not "gemini" || Json.At(m, "tokens") is not { } t)
        {
            return null;
        }
        var cached = Json.Count(t, "cached");
        return LogSlice.Of(
            "gemini",
            "google",
            session,
            Json.Text(m, "model"),
            Json.Timestamp(m),
            (
                Math.Max(Json.Count(t, "input") - cached, 0) + Json.Count(t, "tool"),
                0,
                cached,
                Json.Count(t, "output") + Json.Count(t, "thoughts")
            ),
            since
        );
    }
}
