using System.Text;
using Microsoft.Data.Sqlite;
using Rtok.Store;

namespace Rtok.AotTests;

// The store on NativeAOT (T8): a fresh store applies every migration and every repository method
// runs once. EF builds no model and compiles no query here, so any query the publish did not
// precompile throws instead of passing. Reads go through plain SQLite to check what was written.
public sealed class StoreTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("rtok-aot-store-");
    private readonly Clock _clock = new(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));

    public void Dispose() => _dir.Delete(recursive: true);

    private StoreOptions Options =>
        new(Path.Combine(_dir.FullName, "data", "rtok.db"))
        {
            ArchiveDir = Path.Combine(_dir.FullName, "data", "archive"),
            CallIoInlineBytes = 48,
            Time = _clock,
        };

    [Fact]
    public void Every_repository_method_runs_on_precompiled_queries()
    {
        var options = Options;
        using (var store = RtokStore.Open(options))
        {
            var (call, child, hook) = WriteCalls(store);
            WriteState(store, call);
            AssertWritten(options, call, child, hook);

            _clock.Advance(TimeSpan.FromDays(2));
            Assert.Single(store.ArchivesPendingRetention(1));
            Assert.Equal(1, store.ClearHookBodiesOlderThan(1));
            Assert.Equal(3, store.RunRetention(1, 1));
            Assert.Equal(0, store.PurgeCallsOlderThan(1));
            Assert.Equal(0, store.DropDeadSymbolRoots(null));
            Assert.False(store.ConvertToIncrementalVacuum());
            Assert.Equal(0L, Scalar(options, "SELECT count(*) FROM calls"));
            Assert.Equal(0L, Scalar(options, "SELECT count(*) FROM archive"));
            Assert.Equal(0L, Scalar(options, "SELECT count(*) FROM hook_sessions"));
            Assert.Equal(3L, Scalar(options, "SELECT count(*) FROM usage WHERE call_id IS NULL"));
        }

        Assert.Empty(RtokStore.Housekeeping(options, 30, 7));
        using var again = RtokStore.Open(options);
        Assert.NotNull(again.HostId("codex"));
    }

    private static (long Call, long Child, long Hook) WriteCalls(RtokStore store)
    {
        var claude = store.HostId("claude");
        Assert.NotNull(claude);
        store.UpsertSession("s", claude, "p", "/w", "hook");
        store.UpsertSession("s", null, null, null, "proxy");
        var (provider, model) = store.UpsertModel("acme", "m");
        Assert.Equal((provider, model), store.UpsertModel("acme", "m"));

        var call = store.InsertCall("s", "proxy", "api", claude, provider, model, null, "messages");
        var child = store.InsertCall("s", "proxy", "plugin_run", plugin: "dedup");
        store.SetCallParent(child, call);
        store.SetCallMs(call, 2.5);
        store.SetCallKind(call, "count_tokens");
        store.SetCallServiceTier(call, "priority");
        store.SetCallError(child, "boom");
        store.InsertTokens(call, "dedup", "before", "estimate", 40);
        store.InsertProviderTokens(call, 10, 1, 2, 3, 4);
        store.InsertUsage("s", "m", "messages", 1, 2, 3, 4, call);
        UsageInput Row(string key) => new("m", "batch", 1, 0, 0, 1, key);
        Assert.Equal(2, store.InsertUsageRows("s", call, [Row("a"), Row("a"), Row("b")]));
        Assert.Equal(0, store.InsertUsageRows("s", call, [Row("a")]));
        store.InsertLog("info", "proxy", "n", "msg", "s", call, "dedup");

        var big = Encoding.UTF8.GetBytes(new string('x', 64));
        store.InsertCallIo(call, big, [0xff, 0x00]);
        var hook = store.InsertCall("s", "hook", "hook");
        store.InsertHookCallIo(hook, """{"session_id":"s","prompt":"hi  "}"""u8.ToArray(), null);
        return (call, child, hook);
    }

    private static void WriteState(RtokStore store, long call)
    {
        var m = new MeasurementInput("dedup", "dedup", 100, 10, 25, 3, "ref", call);
        store.InsertMeasurement("s", m, once: "PreToolUse:t1");
        store.InsertMeasurements("s", [m, m with { Kind = "inject" }], once: "PreToolUse:t1");
        Assert.Equal([("claude", 44L)], store.MeasurementSavedByHost(0, long.MaxValue));
        var usage = store.UsageSlices(0, long.MaxValue);
        Assert.All(usage, u => Assert.Equal("claude", u.Host));
        Assert.Equal(14, usage.Sum(u => u.Input + u.CacheCreate + u.CacheRead + u.Output));

        store.KvSet("k", "1");
        store.KvSet("k", "2");
        Assert.Equal("2", store.KvGet("k"));
        store.KvDelete("k");
        Assert.Null(store.KvGet("k"));
        store.PluginStateSet("dedup", "k", "a");
        store.PluginStateSet("dedup", "k", "b");
        Assert.Equal("b", store.PluginStateGet("dedup", "k"));
    }

    private static void AssertWritten(StoreOptions options, long call, long child, long hook)
    {
        Assert.Equal(
            "1|2.5|count_tokens|priority",
            Scalar(
                options,
                $"SELECT ok || '|' || ms || '|' || kind || '|' || service_tier FROM calls WHERE id = {call}"
            )
        );
        Assert.Equal(
            $"0|boom|{call}",
            Scalar(
                options,
                $"SELECT ok || '|' || error || '|' || parent_id FROM calls WHERE id = {child}"
            )
        );
        Assert.Equal(3L, Scalar(options, "SELECT count(*) FROM usage"));
        Assert.Equal(2L, Scalar(options, "SELECT count(*) FROM measurements"));
        Assert.Equal(1L, Scalar(options, "SELECT count(*) FROM archive"));
        Assert.Equal(
            """{"prompt":"hi"}""",
            Scalar(options, $"SELECT request_json FROM call_io WHERE call_id = {hook}")
        );
        Assert.Equal("""{"session_id":"s"}""", Scalar(options, "SELECT fields FROM hook_sessions"));
    }

    [Fact]
    public void A_store_rtok_net_did_not_create_is_refused()
    {
        var options = Options;
        Directory.CreateDirectory(Path.GetDirectoryName(options.DbPath)!);
        Exec(options, "CREATE TABLE calls (id INTEGER PRIMARY KEY);");
        var before = File.ReadAllBytes(options.DbPath);

        Assert.Throws<StoreRefusedException>(() => RtokStore.Open(options));
        Assert.Equal(before, File.ReadAllBytes(options.DbPath));
    }

    private static object? Scalar(StoreOptions options, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={options.DbPath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void Exec(StoreOptions options, string sql) => Scalar(options, sql);

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
