using System.Text;
using Rtok.Config;
using Rtok.Store;
using Rtok.Testing;

namespace Rtok.Report.Tests;

/// <summary>
/// A sandboxed home and a store whose rows make every section and every advice rule speak: one
/// call before the 30-day window, then proxy turns with a tools bust, hook calls (a measured
/// PreToolUse and an idle Stop), cmd, read, inject and graph Measurement rows, and archive
/// pointers two of which were expanded.
/// </summary>
internal sealed class ReportFixture : IDisposable
{
    public const long Now = 1_800_000_000;

    public const string Doctor = "hooks: 2 (fixture)\n";

    private const string Tools =
        """{"system":"You are a coding agent.","tools":[{"name":"Read"}],"messages":[]}""";
    private const string MoreTools =
        """{"system":"You are a coding agent.","tools":[{"name":"Read"},{"name":"Bash"}],"messages":[]}""";

    private readonly TempDir _dir = new("rtok-report-");

    public ReportFixture(bool seed = true)
    {
        Directory.CreateDirectory(Home);
        File.WriteAllText(
            UserFile,
            $"[core]\ndb_path = '{Db}'\narchive_dir = '{_dir.Combine("archive")}'\n"
        );
        Store = RtokStore.Open(
            new StoreOptions(Db) { ArchiveDir = _dir.Combine("archive"), Time = Clock }
        );
        if (seed)
        {
            Seed();
        }
        Clock.Advance(TimeSpan.FromSeconds(Now) - (Clock.GetUtcNow() - DateTimeOffset.UnixEpoch));
    }

    public ManualClock Clock { get; } = new(DateTimeOffset.FromUnixTimeSeconds(Now - (40 * 86400)));

    public RtokStore Store { get; }

    public string Db => _dir.Combine("data", "rtok.db");

    public string Home => Path.Join(_dir.Path, "user", ".rtok");

    public string UserFile => Path.Join(Home, "config.toml");

    public LoadedConfig Load(params ConfigFlag[] flags) =>
        ConfigLoader.Load(
            new ConfigLoadOptions
            {
                Environment = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["HOME"] = Path.Join(_dir.Path, "user"),
                    ["RTOK_HOME"] = Home,
                },
                WorkingDirectory = _dir.Path,
                Flags = flags,
            }
        );

    public ReportDocument Document(params ConfigFlag[] flags) =>
        ReportPage.Build(Load(flags), Clock.GetUtcNow(), Doctor);

    /// <summary>The temp directory, however the OS spells it and markdown escapes it, as
    /// <c>{tmp}</c>.</summary>
    public string Scrub(string text)
    {
        foreach (var path in new[] { Path.GetFullPath(_dir.Path), _dir.Path })
        {
            text = text.Replace(path, "{tmp}", StringComparison.Ordinal)
                .Replace(
                    path.Replace("_", "\\_", StringComparison.Ordinal),
                    "{tmp}",
                    StringComparison.Ordinal
                );
        }
        return text;
    }

    public void Dispose()
    {
        Store.Dispose();
        _dir.Dispose();
    }

    private void Seed()
    {
        Store.UpsertSession("s1", Store.HostId("claude"), "proj", "/work/proj", "claude");
        // Before the window: counted in the totals only.
        Store.InsertCall("s1", "hook", "hook", name: "PreToolUse");
        Clock.Advance(TimeSpan.FromDays(39));
        Proxy();
        Hooks();
        Measure("cmd", "rule", 40_000, 4_000, 10_000, 1_000, "git:a1");
        Measure("cmd", "rule", 20_000, 2_000, 5_000, 500, "cargo:b2");
        Measure("read", "full", 8_000, 2_000, 2_000, 500, "src/big.cs");
        Measure("inject", "inject", 9_000, 9_000, 2_000, 2_000, null);
        Measure("inject", "inject", 9_000, 9_000, 2_000, 2_000, null);
        Measure("graph", "outline", 400, 1_200, 100, 300, null);
        Archive();
    }

    private void Proxy()
    {
        foreach (
            var (body, create, read) in new[]
            {
                (Tools, 30_000L, 0L),
                (Tools, 500L, 30_000L),
                (MoreTools, 31_000L, 200L),
            }
        )
        {
            var id = Store.InsertCall("s1", "proxy", "api_request");
            Store.SetCallMs(id, 900);
            Store.InsertCallIo(id, Encoding.UTF8.GetBytes(body), null);
            Store.InsertUsage("s1", "claude-x", "anthropic", 1_000, create, read, 200, id);
        }
        var mcp = Store.InsertCall("s1", "mcp", "tool", name: "read");
        Store.SetCallMs(mcp, 12.5);
    }

    private void Hooks()
    {
        for (var i = 0; i < 3; i++)
        {
            var id = Store.InsertCall("s1", "hook", "hook", name: "PreToolUse");
            Store.SetCallMs(id, 2 + i);
            Store.InsertMeasurement(
                "s1",
                new MeasurementInput("cmd", "rewrite", 100, 80, 25, 20, CallId: id)
            );
        }
        for (var i = 0; i < Advice.IdleHookCalls; i++)
        {
            Store.InsertCall("s1", "hook", "hook", name: "Stop");
        }
    }

    private void Archive()
    {
        var ids = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            var id = Store.PutArchive("s1", Encoding.UTF8.GetBytes($"tool result {i}\n"));
            Store.PutArchiveDecision("s1", $"tu{i}", id, $"[archived {i}]");
            ids.Add(id);
        }
        foreach (var id in ids.Take(2))
        {
            Store.FreezeRecorded(
                id,
                "expand",
                new MeasurementInput("archive", "expand", 0, 2_000, 0, 500, id)
            );
        }
    }

    private void Measure(
        string plugin,
        string kind,
        long beforeBytes,
        long afterBytes,
        long estBefore,
        long estAfter,
        string? refId
    ) =>
        Store.InsertMeasurement(
            "s1",
            new MeasurementInput(plugin, kind, beforeBytes, afterBytes, estBefore, estAfter, refId)
        );
}
