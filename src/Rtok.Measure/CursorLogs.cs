using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Rtok.Agents.Usage;

namespace Rtok.Measure;

/// <summary>
/// Cursor keeps its chats in the editor's <c>state.vscdb</c> (SQLite, under
/// <c>User/globalStorage</c>): table <c>cursorDiskKV</c>, one JSON value per key. A chat is
/// <c>composerData:&lt;id&gt;</c> (<c>createdAt</c> in unix ms, <c>modelConfig.modelName</c>);
/// each message is <c>bubbleId:&lt;composer&gt;:&lt;bubble&gt;</c>, and an assistant one
/// (<c>type</c> 2) carries <c>tokenCount.inputTokens</c> and <c>outputTokens</c> and, for a tool
/// call, <c>toolFormerData.name</c>. Cursor reports no cache legs.
/// </summary>
public static class CursorLogs
{
    private const string Rows =
        "SELECT key, CAST(value AS TEXT) FROM cursorDiskKV WHERE key LIKE 'composerData:%' OR key LIKE 'bubbleId:%' ORDER BY rowid";

    /// <summary>
    /// The assistant messages of <paramref name="paths"/> stamped at or after
    /// <paramref name="since"/> (unix seconds), with tokens or a tool call; a database that will
    /// not open or read gives none.
    /// </summary>
    public static IReadOnlyList<CallRecord> Read(IEnumerable<string> paths, long since)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var records = new List<CallRecord>();
        foreach (var path in paths.Where(File.Exists))
        {
            try
            {
                records.AddRange(ReadOne(path, since));
            }
            catch (Exception e)
                when (e is SqliteException or InvalidCastException or InvalidOperationException)
            {
                // Not Cursor's database, or locked mid-write: nothing to count.
            }
        }
        return records;
    }

    /// <summary>Read-only and unpooled, as for OpenCode: a running Cursor is never written to.</summary>
    private static List<CallRecord> ReadOne(string path, long since)
    {
        var connection = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(path),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        };
        using var db = new SqliteConnection(connection.ToString());
        db.Open();
        using var query = db.CreateCommand();
        query.CommandText = Rows;
        using var rows = query.ExecuteReader();
        var chats = new Dictionary<string, (long Ts, string? Model)>(StringComparer.Ordinal);
        var bubbles = new List<(string Chat, JsonElement Value)>();
        while (rows.Read())
        {
            if (rows.IsDBNull(1) || Json.Parse(rows.GetString(1)) is not { } v)
            {
                continue;
            }
            var key = rows.GetString(0);
            if (key.StartsWith("composerData:", StringComparison.Ordinal))
            {
                chats[key["composerData:".Length..]] = (
                    Ms(Json.At(v, "createdAt")),
                    Json.Text(v, "modelConfig", "modelName")
                );
            }
            else if (key.Split(':') is [_, var chat, _])
            {
                bubbles.Add((chat, v));
            }
        }
        var records = new List<CallRecord>();
        foreach (var (chat, v) in bubbles)
        {
            chats.TryGetValue(chat, out var c);
            if (Record(chat, v, c.Ts, c.Model) is { } r && (r.Ts == 0 || r.Ts >= since))
            {
                records.Add(r);
            }
        }
        return records;
    }

    /// <summary>One <c>bubbleId</c> value of chat <paramref name="chat"/> as a record, or null.</summary>
    internal static CallRecord? Bubble(string chat, string json) =>
        Json.Parse(json) is { } v ? Record(chat, v, 0, null) : null;

    private static CallRecord? Record(string chat, JsonElement v, long chatTs, string? model)
    {
        if (Json.Integer(v, "type") is not 2)
        {
            return null;
        }
        var input = Json.Count(v, "tokenCount", "inputTokens");
        var output = Json.Count(v, "tokenCount", "outputTokens");
        string[] tools = Json.Text(v, "toolFormerData", "name") is { Length: > 0 } t ? [t] : [];
        if (input + output == 0 && tools.Length == 0)
        {
            return null;
        }
        var ts = Ms(Json.At(v, "createdAt")) is var own and > 0 ? own : chatTs;
        return new CallRecord("cursor", chat, null, model, ts, input, 0, 0, output, tools);
    }

    /// <summary>Unix seconds from unix ms or an ISO 8601 string; 0 for anything else.</summary>
    private static long Ms(JsonElement? v) =>
        v switch
        {
            { ValueKind: JsonValueKind.Number } n when n.TryGetInt64(out var ms) && ms > 0 => ms
                / 1000,
            { ValueKind: JsonValueKind.String } s
                when DateTimeOffset.TryParse(
                    s.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var at
                ) => at.ToUnixTimeSeconds(),
            _ => 0,
        };
}
