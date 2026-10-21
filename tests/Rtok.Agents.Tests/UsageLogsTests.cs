using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Rtok.Agents.Usage;
using Rtok.Config;
using Rtok.Testing;

namespace Rtok.Agents.Tests;

public sealed class UsageLogsTests : IDisposable
{
    private const string Stamp = "2026-10-01T12:00:00Z";
    private static readonly long At = DateTimeOffset
        .Parse(Stamp, CultureInfo.InvariantCulture)
        .ToUnixTimeSeconds();
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Claude_counts_a_streamed_message_once_and_a_subagent_toward_its_parent()
    {
        var line =
            """{"type":"assistant","timestamp":"@0","message":{"id":"m1","model":"claude-test","usage":{"input_tokens":5,"cache_creation_input_tokens":1,"cache_read_input_tokens":2,"output_tokens":3}}}""".Replace(
                "@0",
                Stamp,
                StringComparison.Ordinal
            );
        var synthetic =
            """{"type":"assistant","timestamp":"@0","message":{"id":"m2","model":"<synthetic>","usage":{"input_tokens":0,"output_tokens":0}}}""".Replace(
                "@0",
                Stamp,
                StringComparison.Ordinal
            );
        _dir.Write("p/s1.jsonl", $"{line}\n{line}\n{synthetic}\n");
        _dir.Write("p/s1/subagents/a.jsonl", line);

        var log = ClaudeLogs.Read(_dir.Path, 0);

        Assert.Equal(
            [new UsageSlice("claude", "anthropic", "s1", "claude-test", At, 5, 1, 2, 3)],
            log.Slices.Distinct()
        );
        Assert.Equal(2, log.Slices.Count);
        Assert.Null(log.Unreadable);
    }

    [Fact]
    public void Claude_reports_a_file_with_no_json_line()
    {
        var bad = _dir.Write("p/bad.jsonl", "not json\n{\n");
        _dir.Write("p/empty.jsonl", "\n");

        Assert.Equal(bad, ClaudeLogs.Read(_dir.Path, 0).Unreadable);
    }

    [Fact]
    public void Codex_takes_the_session_meta_id_and_the_latest_model()
    {
        var text = string.Join(
            '\n',
            """{"type":"session_meta","payload":{"id":"sess-1"}}""",
            """{"type":"turn_context","payload":{"model":"gpt-x"}}""",
            """{"type":"event_msg","timestamp":"@0","payload":{"type":"token_count","info":{"last_token_usage":{"input_tokens":10,"cached_input_tokens":4,"output_tokens":2}}}}""".Replace(
                "@0",
                Stamp,
                StringComparison.Ordinal
            ),
            """{"type":"event_msg","timestamp":"@0","payload":{"type":"agent_message","info":{"last_token_usage":{"input_tokens":99}}}}""".Replace(
                "@0",
                Stamp,
                StringComparison.Ordinal
            )
        );

        var rows = CodexLogs.Rollout("rollout-x.jsonl", text, 0);

        Assert.Equal([new UsageSlice("codex", "openai", "sess-1", "gpt-x", At, 6, 0, 4, 2)], rows);
        Assert.Null(CodexLogs.Rollout("r.jsonl", "garbage", 0));
        Assert.Empty(CodexLogs.Rollout("r.jsonl", "", 0)!);
    }

    [Fact]
    public void A_counter_is_never_negative_and_a_huge_one_is_capped()
    {
        var text =
            """{"type":"event_msg","timestamp":"@0","payload":{"type":"token_count","info":{"last_token_usage":{"input_tokens":1,"cached_input_tokens":4,"output_tokens":9223372036854775807,"cache_write_input_tokens":-3}}}}""".Replace(
                "@0",
                Stamp,
                StringComparison.Ordinal
            );

        var rows = CodexLogs.Rollout("r.jsonl", text, 0);

        Assert.Equal(
            [new UsageSlice("codex", "openai", "r", null, At, 0, 0, 4, Json.MaxCount)],
            rows
        );
    }

    [Fact]
    public void Copilot_counts_only_the_last_shutdown_and_prefers_token_details()
    {
        string Shutdown(string metrics) =>
            """{"type":"session.shutdown","timestamp":"@0","data":{"modelMetrics":{@1}}}"""
                .Replace("@0", Stamp, StringComparison.Ordinal)
                .Replace("@1", metrics, StringComparison.Ordinal);
        var text = string.Join(
            '\n',
            Shutdown(""" "old":{"usage":{"inputTokens":1000,"outputTokens":1}} """),
            Shutdown(
                """ "detailed":{"tokenDetails":{"input":{"tokenCount":7},"output":{"tokenCount":3},"cache_read":{"tokenCount":2}},"usage":{"inputTokens":1}}, """
                    + """ "plain":{"usage":{"inputTokens":10,"cacheReadTokens":4,"cacheWriteTokens":1,"outputTokens":2}} """
            )
        );

        var rows = CopilotLogs
            .Session(Path.Combine("state", "sid", "events.jsonl"), text, 0)
            .ToList();

        Assert.Equal(
            [
                new UsageSlice("copilot", "github", "sid", "detailed", At, 7, 0, 2, 3),
                new UsageSlice("copilot", "github", "sid", "plain", At, 5, 1, 4, 2),
            ],
            rows
        );
        Assert.Empty(CopilotLogs.Session("events.jsonl", """{"type":"session.start"}""", 0));
    }

    [Fact]
    public void Gemini_keeps_the_last_record_of_a_message_and_reads_old_documents()
    {
        string Message(int input) =>
            """{"id":"g1","type":"gemini","model":"gemini-x","timestamp":"@0","tokens":{"input":@1,"cached":2,"output":1,"thoughts":1,"tool":1}}"""
                .Replace("@0", Stamp, StringComparison.Ordinal)
                .Replace(
                    "@1",
                    input.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal
                );
        _dir.Write(
            "tmp/p/chats/session-a.jsonl",
            $"{{\"sessionId\":\"sa\"}}\n{Message(5)}\n{Message(9)}\n"
        );
        _dir.Write(
            "tmp/p/chats/session-b.json",
            $"{{\"sessionId\":\"sb\",\"messages\":[{Message(3)}]}}"
        );
        _dir.Write("tmp/p/logs.json", Message(100));
        _dir.Write("tmp/p/chats/checkpoint-a.json", Message(100));

        var log = GeminiLogs.Read([_dir.Combine("tmp")], 0);

        Assert.Equal(
            [
                new UsageSlice("gemini", "google", "sa", "gemini-x", At, 8, 0, 2, 2),
                new UsageSlice("gemini", "google", "sb", "gemini-x", At, 2, 0, 2, 2),
            ],
            log.Slices
        );
    }

    [Fact]
    public void Pi_counts_an_entry_a_fork_copied_once()
    {
        var entry =
            """{"type":"message","id":"e1","timestamp":"@0","message":{"provider":"anthropic","model":"claude-test","usage":{"input":4,"output":1,"cacheRead":2,"cacheWrite":3}}}""".Replace(
                "@0",
                Stamp,
                StringComparison.Ordinal
            );
        var compaction =
            """{"type":"compaction","id":"c1","timestamp":"@0","provider":"openai","usage":{"input":1}}""".Replace(
                "@0",
                Stamp,
                StringComparison.Ordinal
            );
        _dir.Write("a/one.jsonl", $"{{\"type\":\"session\",\"id\":\"s-one\"}}\n{entry}\n");
        _dir.Write("a/two.jsonl", $"{entry}\n{compaction}\n");

        var log = PiLogs.Read([_dir.Combine("a")], 0);

        Assert.Equal(
            [
                new UsageSlice("pi", "anthropic", "s-one", "claude-test", At, 4, 3, 2, 1),
                new UsageSlice("pi", "openai", "two", null, At, 1, 0, 0, 0),
            ],
            log.Slices
        );
    }

    [Fact]
    public void Kimi_sums_every_record_of_a_session_from_its_wire_logs()
    {
        var record =
            """{"type":"usage.record","model":"kimi-x","time":@0,"usage":{"inputOther":3,"inputCacheCreation":1,"inputCacheRead":2,"output":4}}""".Replace(
                "@0",
                (At * 1000 + 999).ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal
            );
        _dir.Write(
            "sessions/key/s9/agents/main/wire.jsonl",
            $"{{\"type\":\"context\"}}\n{record}\n{record}\n"
        );
        _dir.Write("sessions/key/s9/agents/main/other.jsonl", record);

        var log = KimiLogs.Read([_dir.Combine("sessions")], 0);

        Assert.Equal(2, log.Slices.Count);
        Assert.All(
            log.Slices,
            s => Assert.Equal(new UsageSlice("kimi", "moonshot", "s9", "kimi-x", At, 3, 1, 2, 4), s)
        );
    }

    [Fact]
    public void A_host_database_yields_its_assistant_messages_in_the_window()
    {
        var created = At * 1000;
        Database(
            "opencode.db",
            (
                created,
                """{"role":"assistant","providerID":"anthropic","modelID":"claude-test","tokens":{"input":5,"output":2,"reasoning":1,"cache":{"read":3,"write":4}}}"""
            ),
            (created, """{"role":"user","tokens":{"input":50}}"""),
            (created - 86_400_000, """{"role":"assistant","tokens":{"input":70}}""")
        );
        Database(
            "opencode-beta.db",
            (
                created,
                """{"role":"assistant","time":{"created":@0},"tokens":{"input":1}}""".Replace(
                    "@0",
                    (created + 5000).ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal
                )
            )
        );
        Database("opencodex.db", (created, """{"role":"assistant","tokens":{"input":100}}"""));
        _dir.Write("opencode.db.bak", "");
        var before = Snapshot();

        var log = HostDatabases.Read("opencode", [_dir.Path], At - 60);

        Assert.Equal(
            [
                new UsageSlice("opencode", "opencode", "s", null, At + 5, 1, 0, 0, 0),
                new UsageSlice("opencode", "anthropic", "s", "claude-test", At, 5, 4, 3, 3),
            ],
            log.Slices.OrderBy(s => s.Model, StringComparer.Ordinal)
        );
        Assert.Null(log.Unreadable);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public void A_file_that_is_not_a_database_is_reported()
    {
        var bad = _dir.Write("kilo.db", "plain text, not sqlite");

        Assert.Equal(
            bad,
            HostDatabases.Read("kilo", [_dir.Path, _dir.Combine("missing")], 0).Unreadable
        );
    }

    [Fact]
    public void Old_files_are_skipped_and_a_linked_folder_yields_each_file_once()
    {
        var fresh = _dir.Write("logs/p/new.jsonl", "");
        var old = _dir.Write("logs/p/old.jsonl", "");
        File.SetLastWriteTimeUtc(old, DateTime.UnixEpoch.AddSeconds(At - 10));
        File.SetLastWriteTimeUtc(fresh, DateTime.UnixEpoch.AddSeconds(At + 10));
        Directory.CreateSymbolicLink(_dir.Combine("logs", "alias"), _dir.Combine("logs", "p"));
        Directory.CreateSymbolicLink(_dir.Combine("logs", "p", "loop"), _dir.Combine("logs"));

        var found = LogFiles.Find(_dir.Combine("logs"), LogSlice.Cutoff(At), "jsonl");

        Assert.Equal([fresh], found, StringComparer.Ordinal);
    }

    [Fact]
    public void A_bad_line_is_skipped_and_an_undocumented_host_is_named()
    {
        var good = """{"type":"user","timestamp":"@0","message":{"usage":{"input_tokens":1}}}""";
        _dir.Write(
            "claude/p/s.jsonl",
            "{broken\n" + good.Replace("@0", Stamp, StringComparison.Ordinal)
        );
        var config = new RtokConfig();
        config.Stats.TranscriptsDir = _dir.Combine("claude");
        config.Stats.CodexDir = _dir.Combine("none");
        var dirs = config.Agents.Usage.Dirs;
        dirs.Opencode =
            dirs.Kilo =
            dirs.Copilot =
            dirs.Gemini =
            dirs.Pi =
            dirs.Kimi =
                [_dir.Combine("none")];
        string Undocumented(string host)
        {
            _dir.Write($"{host}/any", "");
            return _dir.Combine(host);
        }
        (dirs.Droid, dirs.Grok, dirs.Zcode, dirs.Antigravity) = (
            [_dir.Combine("none"), Undocumented("droid")],
            [Undocumented("grok")],
            [Undocumented("zcode")],
            [Undocumented("antigravity")]
        );

        var logs = UsageLogReader.Read(config.Stats, dirs, 0);

        Assert.Equal(1, logs.Slices.Single().Input);
        Assert.Equal(
            ["droid", "grok", "zcode", "antigravity"],
            logs.Skipped.Select(s => s.Host),
            StringComparer.Ordinal
        );
        Assert.Equal(_dir.Combine("droid"), logs.Skipped[0].Path);
        Assert.All(
            logs.Skipped,
            s => Assert.StartsWith("unsupported: ", s.Reason, StringComparison.Ordinal)
        );
    }

    [Fact]
    public void A_folder_set_in_the_file_is_read_and_an_agent_variable_moves_a_default()
    {
        var record = """{"type":"usage.record","time":@0,"usage":{"output":7}}""".Replace(
            "@0",
            (At * 1000).ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal
        );
        _dir.Write("kimi-home/sessions/k/s1/agents/main/wire.jsonl", record);
        _dir.Write("mine/pi/one.jsonl", $"{{\"timestamp\":\"{Stamp}\",\"usage\":{{\"input\":3}}}}");
        _dir.Write(
            "elsewhere/two.jsonl",
            $"{{\"timestamp\":\"{Stamp}\",\"usage\":{{\"input\":100}}}}"
        );
        var file = _dir.Write(
            "home/config.toml",
            $"[agents.usage.dirs]\npi = [\"{_dir.Combine("mine", "pi")}\"]\n"
        );
        var env = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HOME"] = _dir.Combine("user"),
            ["RTOK_HOME"] = _dir.Combine("home"),
            ["KIMI_CODE_HOME"] = _dir.Combine("kimi-home"),
            ["PI_CODING_AGENT_SESSION_DIR"] = _dir.Combine("elsewhere"),
        };

        var loaded = ConfigLoader.Load(
            new ConfigLoadOptions
            {
                ConfigFile = file,
                WorkingDirectory = _dir.Path,
                Environment = env,
            }
        );
        var dirs = loaded.Config.Agents.Usage.Dirs;

        Assert.Empty(loaded.Issues);
        Assert.Equal(3, PiLogs.Read(dirs.Pi, 0).Slices.Single().Input);
        Assert.Equal(7, KimiLogs.Read(dirs.Kimi, 0).Slices.Single().Output);
    }

    /// <summary>Every file in the test folder with its hash: a read leaves no journal and changes no byte.</summary>
    private Dictionary<string, string> Snapshot() =>
        Directory
            .EnumerateFiles(_dir.Path)
            .ToDictionary(
                p => p,
                p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))),
                StringComparer.Ordinal
            );

    private void Database(string name, params (long Created, string Data)[] rows)
    {
        var connection = new SqliteConnectionStringBuilder
        {
            DataSource = _dir.Combine(name),
            Pooling = false,
        };
        using var db = new SqliteConnection(connection.ToString());
        db.Open();
        using var create = db.CreateCommand();
        create.CommandText =
            "CREATE TABLE message (id INTEGER PRIMARY KEY, session_id TEXT, time_created INTEGER, data TEXT)";
        create.ExecuteNonQuery();
        foreach (var (created, data) in rows)
        {
            using var insert = db.CreateCommand();
            insert.CommandText =
                "INSERT INTO message (session_id, time_created, data) VALUES ('s', $c, $d)";
            insert.Parameters.AddWithValue("$c", created);
            insert.Parameters.AddWithValue("$d", data);
            insert.ExecuteNonQuery();
        }
    }
}
