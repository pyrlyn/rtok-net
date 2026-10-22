using System.Text;
using Rtok.Store.Model;
using Rtok.Testing;

namespace Rtok.Store.Tests;

public sealed class RetentionTests
{
    private static readonly TimeSpan TwoDays = TimeSpan.FromDays(2);

    private static StoreHarness Harness()
    {
        var h = new StoreHarness(o => o with { CallIoInlineBytes = 16 });
        h.Store.UpsertSession("s", null, null, null, null);
        return h;
    }

    private static byte[] Body(char c) => Encoding.UTF8.GetBytes(new string(c, 64));

    [Fact]
    public void Old_calls_go_with_what_only_describes_them()
    {
        using var h = Harness();
        var store = h.Store;
        var old = store.InsertCall("s", "proxy", "api");
        var shared = store.InsertCall("s", "proxy", "api");
        var held = store.InsertCall("s", "proxy", "api");
        store.InsertCallIo(old, Body('a'), null);
        store.InsertCallIo(shared, Body('b'), null);
        store.InsertCallIo(held, Body('c'), null);
        store.InsertTokens(old, null, "before", "estimate", 1);
        store.InsertLog("info", "proxy", "n", "old, no call");
        store.InsertLog("info", "proxy", "n", "old call", callId: old);
        var u = Fakes.Usage().Generate();
        store.InsertUsage("s", u.Model, u.Api, u.Input, u.CacheCreate, u.CacheRead, u.Output, old);
        store.InsertMeasurement("s", Fakes.Measurement().Generate() with { CallId = old });
        using (var db = h.Read())
        {
            db.ArchiveDecisions.Add(
                new ArchiveDecision
                {
                    Session = "s",
                    ToolUseId = "t",
                    ArchiveId = StoreFiles.Sha256Hex(Body('c')),
                    Pointer = "p",
                    Ts = 0,
                }
            );
            db.SaveChanges();
        }
        h.Clock.Advance(TwoDays);
        var fresh = store.InsertCall("s", "proxy", "api");
        var child = store.InsertCall("s", "proxy", "plugin_run");
        store.SetCallParent(child, old);
        store.InsertCallIo(fresh, Body('b'), null);
        store.InsertLog("info", "proxy", "n", "new");
        var a = StoreFiles.ArchivePath(h.Options.ArchiveDir!, StoreFiles.Sha256Hex(Body('a')));

        Assert.Equal([a], store.ArchivesPendingRetention(1));
        Assert.Empty(store.ArchivesPendingRetention(0));
        Assert.Equal(0, store.PurgeCallsOlderThan(0));
        Assert.Equal(3, store.PurgeCallsOlderThan(1));

        Assert.False(File.Exists(a));
        using var read = h.Read();
        Assert.Equal([fresh, child], read.Calls.Select(c => c.Id).OrderBy(id => id).ToList());
        Assert.Equal([fresh], read.CallIo.Select(io => io.CallId).ToList());
        Assert.Equal(2, read.Archive.Count());
        Assert.All(read.Archive.ToList(), x => Assert.True(File.Exists(x.Path)));
        Assert.Empty(read.Tokens);
        Assert.Equal(["new"], read.Logs.Select(l => l.Message).ToList());
        Assert.Null(read.Usage.Single().CallId);
        Assert.Null(read.Measurements.Single().CallId);
        Assert.Null(read.Calls.Single(c => c.Id == child).ParentId);
    }

    [Fact]
    public void Old_hook_bodies_are_cleared_in_batches()
    {
        using var h = new StoreHarness();
        var store = h.Store;
        store.UpsertSession("s", null, null, null, null);
        var hooks = Enumerable
            .Range(0, 3)
            .Select(_ => store.InsertCall("s", "hook", "hook"))
            .ToList();
        foreach (var call in hooks)
        {
            store.InsertHookCallIo(
                call,
                """{"session_id":"s","prompt":"hi"}"""u8.ToArray(),
                "{}"u8.ToArray()
            );
        }
        var api = store.InsertCall("s", "proxy", "api");
        store.InsertCallIo(api, "{}"u8.ToArray(), "{}"u8.ToArray());
        h.Clock.Advance(TwoDays);
        var fresh = store.InsertCall("s", "hook", "hook");
        store.InsertHookCallIo(fresh, """{"session_id":"t","prompt":"hi"}"""u8.ToArray(), null);

        Assert.Equal(3, store.ClearHookBodies(1, batch: 2));
        Assert.Equal(0, store.ClearHookBodiesOlderThan(1));
        Assert.Equal(0, store.RunRetention(0, 1));

        using var db = h.Read();
        foreach (var io in db.CallIo.Where(io => hooks.Contains(io.CallId)).ToList())
        {
            Assert.Equal((null, null, null), (io.RequestJson, io.ResponseJson, io.HookSessionId));
            Assert.NotNull(io.RequestSha256);
        }
        Assert.NotNull(db.CallIo.Single(io => io.CallId == api).RequestJson);
        Assert.NotNull(db.CallIo.Single(io => io.CallId == fresh).RequestJson);
        Assert.Equal(["""{"session_id":"t"}"""], db.HookSessions.Select(s => s.Fields).ToList());
    }

    [Fact]
    public void Symbols_of_vanished_roots_are_dropped()
    {
        using var h = Harness();
        using var live = new TempDir("rtok-root-");
        var home = h.Combine("home");
        Directory.CreateDirectory(home);
        var gone = h.Combine("gone");
        using (var db = h.Read())
        {
            foreach (
                var root in (string[])[live.Path, gone, home, Path.GetPathRoot(live.Path)!, ""]
            )
            {
                db.Symbols.Add(Fakes.Symbol(root));
            }
            db.SymbolStale.Add(new SymbolStale { Root = gone, Path = "a.cs" });
            db.Extractor.Add(new Extractor { Root = gone, Fingerprint = "f" });
            db.SaveChanges();
        }

        Assert.Equal(3, h.Store.DropDeadSymbolRoots(home));

        using var read = h.Read();
        Assert.Equal(["", live.Path], read.Symbols.Select(s => s.Root).OrderBy(r => r).ToList());
        Assert.Empty(read.SymbolStale);
        Assert.Empty(read.Extractor);
    }

    [Fact]
    public void An_older_file_switches_to_incremental_vacuum_once()
    {
        using var h = Harness();
        Assert.False(h.Store.ConvertToIncrementalVacuum());
        h.Reopen();
        OpenTests.Exec(h.Options.DbPath, "PRAGMA auto_vacuum = NONE; VACUUM;");
        Assert.Equal(0L, OpenTests.Pragma(h.Options.DbPath, "auto_vacuum"));

        Assert.True(h.Store.ConvertToIncrementalVacuum());
        Assert.False(h.Store.ConvertToIncrementalVacuum());
        Assert.Equal(2L, OpenTests.Pragma(h.Options.DbPath, "auto_vacuum"));
    }

    [Fact]
    public void Housekeeping_reports_failures_as_warnings()
    {
        using var h = Harness();
        Assert.Empty(RtokStore.Housekeeping(h.Options, 30, 7));
        h.Reopen();
        File.Delete(h.Options.DbPath + "-wal");
        File.Delete(h.Options.DbPath + "-shm");
        File.Delete(h.Options.DbPath);
        OpenTests.Exec(h.Options.DbPath, "CREATE TABLE calls (id INTEGER PRIMARY KEY);");

        var warning = Assert.Single(RtokStore.Housekeeping(h.Options, 30, 7));

        Assert.Equal("retention", warning.What);
        Assert.StartsWith(
            "retention skipped until next start: ",
            warning.Message,
            StringComparison.Ordinal
        );
    }
}
