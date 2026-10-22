using Microsoft.Data.Sqlite;
using Rtok.Testing;

namespace Rtok.Measure.Tests;

/// <summary>Each host's session logs as call records, on the shared fixtures and on databases built here.</summary>
public sealed class CallRecordsTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public Task Claude_bills_sub_agents_to_their_parent()
    {
        var records = CallRecords.Claude(RepoPaths.Fixture("stats", "claude"), DateTime.UnixEpoch);

        Assert.All(records, r => Assert.Equal("sess-1", r.Session));
        Assert.Equal(2, records.Count(r => r.Agent is "Explore"));
        return Verify(records);
    }

    [Fact]
    public Task Codex_rollouts() =>
        Verify(CallRecords.Codex(RepoPaths.Fixture("usage-logs", "codex"), 0));

    [Fact]
    public Task OpenCode_assistant_messages()
    {
        Sql(
            "opencode.db",
            "CREATE TABLE message (id INTEGER PRIMARY KEY, session_id TEXT, time_created INTEGER, data TEXT)",
            (
                "INSERT INTO message (session_id, time_created, data) VALUES ('ses_1', 1790000000000, $v)",
                """{"role":"user","time":{"created":1790000000000}}"""
            ),
            (
                "INSERT INTO message (session_id, time_created, data) VALUES ('ses_1', 1790000005000, $v)",
                """{"role":"assistant","providerID":"anthropic","modelID":"claude-sonnet-5-5","time":{"created":1790000005000},"tokens":{"input":120,"output":40,"reasoning":10,"cache":{"read":9000,"write":300}}}"""
            )
        );

        return Verify(CallRecords.OpenCode([_dir.Path], 0));
    }

    [Fact]
    public Task Cursor_bubbles_with_tokens_or_a_tool()
    {
        Sql(
            "state.vscdb",
            "CREATE TABLE cursorDiskKV (key TEXT UNIQUE ON CONFLICT REPLACE, value BLOB)",
            Kv(
                "composerData:c1",
                """{"composerId":"c1","createdAt":1790000000000,"modelConfig":{"modelName":"claude-4.5-sonnet"}}"""
            ),
            Kv(
                "bubbleId:c1:b1",
                """{"type":1,"text":"why is the build red?","tokenCount":{"inputTokens":0,"outputTokens":0}}"""
            ),
            Kv(
                "bubbleId:c1:b2",
                """{"type":2,"text":"","tokenCount":{"inputTokens":5400,"outputTokens":120},"toolFormerData":{"name":"read_file"}}"""
            ),
            Kv(
                "bubbleId:c1:b3",
                """{"type":2,"text":"The test fixture moved.","createdAt":"2026-09-21T10:00:00Z","tokenCount":{"inputTokens":6100,"outputTokens":80}}"""
            ),
            Kv(
                "bubbleId:c1:b4",
                """{"type":2,"text":"","tokenCount":{"inputTokens":0,"outputTokens":0}}"""
            ),
            Kv("bubbleId:c2:b1", """{"type":2,"tokenCount":{"inputTokens":10,"outputTokens":2}}"""),
            Kv("ItemTable:other", "not json")
        );

        return Verify(CursorLogs.Read([_dir.Combine("state.vscdb")], 0));
    }

    [Fact]
    public void Cursor_skips_old_bubbles_and_foreign_databases()
    {
        Sql(
            "state.vscdb",
            "CREATE TABLE cursorDiskKV (key TEXT, value BLOB)",
            Kv("composerData:c1", """{"createdAt":1000000000000}"""),
            Kv("bubbleId:c1:b1", """{"type":2,"tokenCount":{"inputTokens":10,"outputTokens":2}}""")
        );
        Sql("other.db", "CREATE TABLE t (x INTEGER)");

        Assert.Empty(
            CursorLogs.Read(
                [_dir.Combine("state.vscdb"), _dir.Combine("other.db"), _dir.Combine("none.db")],
                1_700_000_000
            )
        );
    }

    private static (string Sql, string Value) Kv(string key, string value) =>
        ($"INSERT INTO cursorDiskKV (key, value) VALUES ('{key}', $v)", value);

    private void Sql(string name, string create, params (string Sql, string Value)[] rows)
    {
        var connection = new SqliteConnectionStringBuilder
        {
            DataSource = _dir.Combine(name),
            Pooling = false,
        };
        using var db = new SqliteConnection(connection.ToString());
        db.Open();
        using (var c = db.CreateCommand())
        {
            c.CommandText = create;
            c.ExecuteNonQuery();
        }
        foreach (var (sql, value) in rows)
        {
            using var insert = db.CreateCommand();
            insert.CommandText = sql;
            insert.Parameters.AddWithValue("$v", value);
            insert.ExecuteNonQuery();
        }
    }
}
