using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Rtok.Agents.Usage;

/// <summary>
/// OpenCode and Kilo Code CLI keep their sessions in SQLite, <c>&lt;host&gt;.db</c> (or
/// <c>&lt;host&gt;-&lt;channel&gt;.db</c> for a non-stable release channel; the backups next to
/// them end in something else). Each row of its <c>message</c> table holds one message as JSON;
/// an assistant message carries <c>tokens</c>, <c>providerID</c>, <c>modelID</c> and
/// <c>time.created</c>. Kilo is a fork of OpenCode and shares the schema.
/// </summary>
internal static class HostDatabases
{
    private const string Messages =
        "SELECT session_id, time_created, data FROM message WHERE time_created >= $since ORDER BY time_created";

    public static HostLog Read(string host, IEnumerable<string> dirs, long since)
    {
        var slices = new List<UsageSlice>();
        string? unreadable = null;
        foreach (var path in Databases(host, dirs))
        {
            try
            {
                slices.AddRange(ReadOne(host, path, since));
            }
            catch (Exception e)
                when (e
                        is SqliteException
                            or InvalidCastException
                            or FormatException
                            or InvalidOperationException
                )
            {
                unreadable ??= path;
            }
        }
        return new HostLog(slices, unreadable);
    }

    /// <summary>The host's databases directly in <paramref name="dirs"/>, sorted.</summary>
    internal static List<string> Databases(string host, IEnumerable<string> dirs)
    {
        var found = new List<string>();
        foreach (var dir in dirs)
        {
            try
            {
                found.AddRange(
                    Directory.EnumerateFiles(dir).Where(p => IsDatabase(host, Path.GetFileName(p)))
                );
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A missing or unreadable folder holds no database.
            }
        }
        found.Sort(StringComparer.Ordinal);
        return found;
    }

    internal static bool IsDatabase(string host, string name) =>
        name.EndsWith(".db", StringComparison.Ordinal)
        && name[..^3] is var stem
        && stem.StartsWith(host, StringComparison.Ordinal)
        && (stem.Length == host.Length || stem[host.Length] == '-');

    /// <summary>
    /// Opened read-only, so a running host is never written to, not even a journal checkpoint,
    /// and unpooled, so the file is released as soon as it is read.
    /// </summary>
    private static List<UsageSlice> ReadOne(string host, string path, long since)
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
        query.CommandText = Messages;
        query.Parameters.AddWithValue(
            "$since",
            since > long.MaxValue / 1000 ? long.MaxValue : since * 1000
        );
        using var rows = query.ExecuteReader();
        var slices = new List<UsageSlice>();
        while (rows.Read())
        {
            var session = rows.IsDBNull(0) ? "" : rows.GetString(0);
            var created = rows.IsDBNull(1) ? 0 : rows.GetInt64(1);
            if (
                !rows.IsDBNull(2)
                && Request(host, session, created, rows.GetString(2), since) is { } slice
            )
            {
                slices.Add(slice);
            }
        }
        return slices;
    }

    /// <summary>
    /// An assistant message as one request. <c>input</c> and <c>output</c> already exclude the
    /// cache legs and the reasoning tokens (the host's <c>getUsage</c>), so the four legs and the
    /// reasoning sum to the total; reasoning is billed as output.
    /// </summary>
    internal static UsageSlice? Request(
        string host,
        string session,
        long createdMs,
        string data,
        long since
    )
    {
        if (
            Json.Parse(data) is not { } v
            || Json.Text(v, "role") is not "assistant"
            || Json.At(v, "tokens") is not { } t
        )
        {
            return null;
        }
        var ms = Json.Integer(v, "time", "created") ?? createdMs;
        return LogSlice.Of(
            host,
            Json.Text(v, "providerID") ?? host,
            session,
            Json.Text(v, "modelID"),
            ms / 1000,
            (
                Json.Count(t, "input"),
                Json.Count(t, "cache", "write"),
                Json.Count(t, "cache", "read"),
                Json.Count(t, "output") + Json.Count(t, "reasoning")
            ),
            since
        );
    }
}
