namespace Rtok.Store.Tests;

public sealed class WriteTests
{
    [Fact]
    public void A_session_keeps_what_an_earlier_writer_set()
    {
        using var h = new StoreHarness();
        var f = Fakes.Values();
        var (sProject, sCwd) = (f.Lorem.Word(), f.System.DirectoryPath());
        var (tProject, tCwd) = (f.Lorem.Word(), f.System.DirectoryPath());
        var claude = h.Store.HostId("claude");
        h.Store.UpsertSession("s", claude, sProject, sCwd, "hook");
        h.Store.UpsertSession("s", null, null, null, "proxy");
        h.Store.UpsertSession("t", null, null, null, "proxy");
        h.Store.UpsertSession("t", claude, tProject, tCwd, "hook");

        using var db = h.Read();
        var s = db.Sessions.Single(x => x.Id == "s");
        Assert.Equal((claude, sProject, sCwd, "hook"), (s.HostId, s.Project, s.Cwd, s.SourceName));
        Assert.Equal(1_800_000_000, s.StartedAt);
        var t = db.Sessions.Single(x => x.Id == "t");
        Assert.Equal((claude, tProject, tCwd, "proxy"), (t.HostId, t.Project, t.Cwd, t.SourceName));
    }

    [Fact]
    public void A_model_is_inserted_once_per_provider()
    {
        using var h = new StoreHarness();

        var first = h.Store.UpsertModel("acme", "m1");
        var again = h.Store.UpsertModel("acme", "m1");
        var other = h.Store.UpsertModel("acme", "m2");
        var elsewhere = h.Store.UpsertModel("zeta", "m1");

        Assert.Equal(first, again);
        Assert.Equal(first.ProviderId, other.ProviderId);
        Assert.NotEqual(first.ModelId, other.ModelId);
        Assert.NotEqual(first.ProviderId, elsewhere.ProviderId);
        using var db = h.Read();
        Assert.Equal("acme", db.Providers.Single(p => p.Id == first.ProviderId).Name);
    }

    [Fact]
    public void A_call_takes_every_later_detail()
    {
        using var h = new StoreHarness();
        var f = Fakes.Values();
        var (ms, error) = (Math.Round(f.Random.Double(1, 5_000), 1), f.Lorem.Sentence());
        h.Store.UpsertSession("s", null, null, null, null);
        var (provider, model) = h.Store.UpsertModel("acme", "m");
        var parent = h.Store.InsertCall(
            "s",
            "proxy",
            "api",
            h.Store.HostId("claude"),
            provider,
            model,
            null,
            "messages"
        );
        var child = h.Store.InsertCall("s", "proxy", "plugin_run", plugin: "dedup");

        h.Store.SetCallParent(child, parent);
        h.Store.SetCallMs(parent, ms);
        h.Store.SetCallKind(parent, "count_tokens");
        h.Store.SetCallServiceTier(parent, "priority");
        h.Store.SetCallError(child, error);

        using var db = h.Read();
        var p = db.Calls.Single(c => c.Id == parent);
        Assert.Equal(
            (ms, "count_tokens", "priority", true, null),
            (p.Ms, p.Kind, p.ServiceTier, p.Ok, p.Error)
        );
        Assert.Equal(
            (provider, model, "messages"),
            (p.ProviderId!.Value, p.ModelId!.Value, p.Name)
        );
        var c = db.Calls.Single(x => x.Id == child);
        Assert.Equal((parent, false, error, "dedup"), (c.ParentId!.Value, c.Ok, c.Error, c.Plugin));
    }

    [Fact]
    public void Tokens_logs_and_usage_land_as_given()
    {
        using var h = new StoreHarness();
        var f = Fakes.Values();
        var (estimate, message) = (f.Random.Long(1, 100_000), f.Lorem.Sentence());
        var u = Fakes.Usage().Generate();
        var total = u.Input + u.CacheCreate + u.CacheRead + u.Output;
        h.Store.UpsertSession("s", null, null, null, null);
        var call = h.Store.InsertCall("s", "proxy", "api");

        h.Store.InsertTokens(call, "dedup", "before", "estimate", estimate);
        h.Store.InsertProviderTokens(call, total, u.Input, u.CacheCreate, u.CacheRead, u.Output);
        h.Store.InsertUsage(
            "s",
            u.Model,
            u.Api,
            u.Input,
            u.CacheCreate,
            u.CacheRead,
            u.Output,
            call
        );
        h.Store.InsertLog("warn", "proxy", "upstream", message, "s", call, "dedup");

        using var db = h.Read();
        var tokens = db.Tokens.OrderBy(t => t.Id).ToList();
        Assert.Equal(
            ("dedup", "before", "estimate", estimate),
            (tokens[0].Plugin, tokens[0].Phase, tokens[0].SourceName, tokens[0].Tokens)
        );
        Assert.Equal(
            ("after", "provider", total, u.Input, u.CacheCreate, u.CacheRead, u.Output),
            (
                tokens[1].Phase,
                tokens[1].SourceName,
                tokens[1].Tokens,
                tokens[1].Input!.Value,
                tokens[1].CacheCreate!.Value,
                tokens[1].CacheRead!.Value,
                tokens[1].Output!.Value
            )
        );
        var usage = db.Usage.Single();
        Assert.Equal(
            (u.Model, u.Api, u.Input, u.CacheCreate, u.CacheRead, u.Output, call),
            (
                usage.Model,
                usage.Api,
                usage.Input,
                usage.CacheCreate,
                usage.CacheRead,
                usage.Output,
                usage.CallId!.Value
            )
        );
        var log = db.Logs.Single();
        Assert.Equal(
            ("warn", "proxy", "upstream", message, "dedup"),
            (log.Level, log.SourceName, log.Name, log.Message, log.Plugin)
        );
    }

    [Fact]
    public void Usage_rows_count_each_result_once()
    {
        using var h = new StoreHarness();
        h.Store.UpsertSession("s", null, null, null, null);
        var call = h.Store.InsertCall("s", "proxy", "batch");
        var usage = Fakes.Usage().Generate();
        UsageInput Row(string? key) => usage with { ResultKey = key };

        Assert.Equal(
            3,
            h.Store.InsertUsageRows("s", call, [Row("a"), Row("a"), Row("b"), Row(null)])
        );
        Assert.Equal(1, h.Store.InsertUsageRows("s", call, [Row("a"), Row("b"), Row(null)]));

        using var db = h.Read();
        Assert.Equal(4, db.Usage.Count());
        Assert.Equal(2, db.Usage.Count(u => u.ResultKey == null));
    }

    [Fact]
    public void A_measurement_delivered_twice_counts_once()
    {
        using var h = new StoreHarness();
        h.Store.UpsertSession("s", null, null, null, null);
        var m = Fakes.Measurement().Generate();
        var other = m with { Kind = m.Kind + "-other" };

        h.Store.InsertMeasurement("s", m, once: "PreToolUse:t1");
        h.Store.InsertMeasurements("s", [m, other], once: "PreToolUse:t1");
        h.Store.InsertMeasurement("s", m, once: "PreToolUse:t2");
        h.Store.InsertMeasurement("s", m);
        h.Store.InsertMeasurement("s", m);

        using var db = h.Read();
        Assert.Equal(5, db.Measurements.Count());
        Assert.Equal(
            [
                // "-" sorts before "|", so the longer kind comes first.
                $"PreToolUse:t1|{m.Plugin}|{other.Kind}|{m.RefId}",
                $"PreToolUse:t1|{m.Plugin}|{m.Kind}|{m.RefId}",
                $"PreToolUse:t2|{m.Plugin}|{m.Kind}|{m.RefId}",
            ],
            db.Measurements.Where(x => x.OnceKey != null)
                .Select(x => x.OnceKey!)
                .OrderBy(k => k)
                .ToList()
        );
        var row = db.Measurements.First();
        Assert.Equal(
            (m.BeforeBytes, m.AfterBytes, m.EstBefore, m.EstAfter, m.RefId),
            (row.BeforeBytes, row.AfterBytes, row.EstBefore, row.EstAfter, row.RefId)
        );
    }

    [Fact]
    public void Kv_and_plugin_state_round_trip()
    {
        using var h = new StoreHarness();

        Assert.Null(h.Store.KvGet("k"));
        h.Store.KvSet("k", "1");
        h.Store.KvSet("k", "2");
        Assert.Equal("2", h.Store.KvGet("k"));
        h.Store.KvDelete("k");
        h.Store.KvDelete("k");
        Assert.Null(h.Store.KvGet("k"));

        Assert.Null(h.Store.PluginStateGet("p", "k"));
        h.Store.PluginStateSet("p", "k", "a");
        h.Store.PluginStateSet("p", "k", "b");
        h.Store.PluginStateSet("q", "k", "c");
        Assert.Equal("b", h.Store.PluginStateGet("p", "k"));
        Assert.Equal("c", h.Store.PluginStateGet("q", "k"));
    }
}
