using System.Diagnostics;
using System.Threading.Channels;

namespace Rtok.Plugins.Graph.Tests;

// rtok graph/watch.rs: the debounce, which events count, and one real watcher end to end.
public sealed class WatchTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    private static WatchState State(GraphHarness h) =>
        new(h.Cx, new Matcher(h.Project.Path, Indexer.Config(h.Cx))) { Last = T0 };

    // The watcher reports paths under the canonical root it watches.
    private static WatchEvent Ev(GraphHarness h, string relative) =>
        new(Path.Join(h.Root, relative));

    private static List<string> Defs(GraphHarness h, string name) =>
        [.. h.Store.SymbolDefs(h.Root, name).Select(d => d.Path)];

    // Feeds `sent` to the loop on the real clock and stops it a quiet window after `want` runs.
    private static int PumpEvents(GraphHarness h, IEnumerable<WatchEvent> sent, int want)
    {
        var events = Channel.CreateUnbounded<WatchEvent>();
        foreach (var ev in sent)
        {
            events.Writer.TryWrite(ev);
        }

        using var stop = new CancellationTokenSource();
        var state = State(h);
        state.Last = DateTimeOffset.UtcNow;
        var loop = new Thread(() => GraphWatch.Pump(state, events.Reader, stop.Token));
        loop.Start();
        var cap = Stopwatch.StartNew();
        while (state.Runs < want && cap.Elapsed < TimeSpan.FromSeconds(10))
        {
            Thread.Sleep(20);
        }

        // One more quiet window: a surplus run would land in it.
        Thread.Sleep(WatchState.Quiet + TimeSpan.FromMilliseconds(200));
        stop.Cancel();
        loop.Join();
        return state.Runs;
    }

    [Fact]
    public void Settle_runs_once_after_the_quiet_window_and_never_when_clean()
    {
        using var h = new GraphHarness();
        h.Write("a.rs", "pub fn settled() {}\n");
        var state = State(h);

        Assert.True(state.Absorb(Ev(h, "a.rs"), T0));
        Assert.False(state.Settle(T0 + TimeSpan.FromMilliseconds(100)));
        Assert.Equal((0, 1), (state.Runs, state.Pending.Count));
        Assert.True(state.Settle(T0 + WatchState.Quiet));
        Assert.Equal((1, 0, false), (state.Runs, state.Pending.Count, state.Rescan));
        Assert.Equal(["a.rs"], Defs(h, "settled"));
        Assert.False(state.Settle(T0 + WatchState.Quiet * 3));
        Assert.Equal(1, state.Runs);
    }

    [Fact]
    public void Git_churn_and_unsupported_files_neither_wait_nor_clear_what_waits()
    {
        using var h = new GraphHarness();
        h.Write("a.rs", "pub fn seed() {}\n");
        h.Write(".git/index", "x");
        h.Write("rtok.db-wal", "x");
        h.Write("notes.md", "x");
        var state = State(h);

        Assert.True(state.Absorb(Ev(h, "a.rs"), T0));
        foreach (var path in (string[])[".git/index", ".git/HEAD", "rtok.db-wal", "notes.md"])
        {
            Assert.False(state.Absorb(Ev(h, path), T0 + TimeSpan.FromSeconds(1)));
        }

        Assert.Equal(T0, state.Last);
        Assert.False(state.Rescan);
        Assert.Single(state.Pending);
    }

    [Fact]
    public void Events_follow_the_walks_ignore_include_and_exclude_rules()
    {
        using var h = new GraphHarness("""{"include":["vendor/kept/"],"exclude":["**/gen/**"]}""");
        Directory.CreateDirectory(h.Project.Combine(".git"));
        h.Write(".gitignore", "target/\nvendor/\n");
        foreach (
            var path in (string[])
                [
                    "target/out.rs",
                    "vendor/kept/k.rs",
                    "vendor/drop/d.rs",
                    "src/gen/g.rs",
                    "src/s.rs",
                ]
        )
        {
            h.Write(path, "fn f() {}\n");
        }

        var state = State(h);
        foreach (
            var path in (string[])
                [
                    "target/out.rs",
                    "target/gone/x.rs",
                    "vendor/kept/k.rs",
                    "vendor/drop/d.rs",
                    "src/gen/g.rs",
                    "src/s.rs",
                ]
        )
        {
            state.Absorb(Ev(h, path), T0);
        }

        Assert.Equal(
            ["src/s.rs", "vendor/kept/k.rs"],
            state
                .Pending.Select(p => Path.GetRelativePath(h.Root, p).Replace('\\', '/'))
                .Order(StringComparer.Ordinal),
            StringComparer.Ordinal
        );
        Assert.False(state.Rescan);
    }

    [Fact]
    public void Events_inside_a_nested_linked_worktree_are_another_index()
    {
        using var h = new GraphHarness();
        Directory.CreateDirectory(h.Project.Combine(".git", "worktrees", "x"));
        File.WriteAllText(h.Project.Combine(".git", "worktrees", "x", "commondir"), "../..\n");
        h.Write(
            ".rtok/worktrees/x/.git",
            $"gitdir: {h.Project.Combine(".git", "worktrees", "x")}\n"
        );
        h.Write(".rtok/worktrees/x/src/n.rs", "fn n() {}\n");
        h.Write("own.rs", "fn own() {}\n");
        var state = State(h);

        Assert.False(state.Absorb(Ev(h, ".rtok/worktrees/x/src/n.rs"), T0));
        Assert.False(state.Absorb(Ev(h, ".rtok/worktrees/x/src"), T0));
        Assert.True(state.Absorb(Ev(h, "own.rs"), T0));
        Assert.False(state.Rescan);
    }

    [Fact]
    public void A_removed_directory_or_an_overflow_walks_the_root_once()
    {
        using var h = new GraphHarness();
        h.Write("src/module/gone.rs", "pub fn vanished() {}\n");
        h.Run();
        Directory.Delete(h.Project.Combine("src", "module"), recursive: true);
        var state = State(h);

        Assert.True(state.Absorb(Ev(h, "src/module"), T0));
        Assert.True(state.Rescan);
        Assert.True(state.Settle(T0 + WatchState.Quiet));
        Assert.Empty(Defs(h, "vanished"));
        Assert.Equal(0, Indexer.Ensure(h.Cx, h.Project.Path).Read);

        Assert.True(state.Absorb(new WatchEvent(null, Rescan: true), T0));
        Assert.True(state.Rescan);
        for (var i = 0; i <= WatchState.PendingCap; i++)
        {
            state.Absorb(Ev(h, $"f{i}.rs"), T0);
        }

        Assert.True(state.Rescan);
        Assert.Empty(state.Pending);
    }

    [Fact]
    public void An_edit_and_a_rename_reindex_once_each()
    {
        using var h = new GraphHarness();
        var a = h.Write("a.rs", "pub fn old_name() {}\n");
        h.Run();
        // A different length, so the stat gate cannot call the edit unchanged.
        File.WriteAllText(a, "pub fn new_name_longer() {}\n");

        Assert.Equal(1, PumpEvents(h, [Ev(h, "a.rs")], 1));
        Assert.Equal(["a.rs"], Defs(h, "new_name_longer"));
        Assert.Empty(Defs(h, "old_name"));
        File.Move(a, h.Project.Combine("b.rs"));
        Assert.Equal(1, PumpEvents(h, [Ev(h, "a.rs"), Ev(h, "b.rs")], 1));
        Assert.Equal(["b.rs"], Defs(h, "new_name_longer"));
    }

    [Fact]
    public void A_burst_runs_once_and_noise_never_runs()
    {
        using var h = new GraphHarness();
        h.Write("a.rs", "pub fn seed() {}\n");

        Assert.Equal(0, PumpEvents(h, [Ev(h, ".git/index"), Ev(h, ".git/HEAD")], 0));
        Assert.Equal(1, PumpEvents(h, Enumerable.Repeat(Ev(h, "a.rs"), 50), 1));
    }

    [Fact]
    public void The_loop_ends_on_stop_or_on_a_closed_channel()
    {
        using var h = new GraphHarness();
        var events = Channel.CreateUnbounded<WatchEvent>();
        Assert.Equal(
            0,
            GraphWatch.Pump(h.Cx, h.Project.Path, events.Reader, new CancellationToken(true)).Runs
        );
        events.Writer.Complete();
        Assert.Equal(
            0,
            GraphWatch.Pump(h.Cx, h.Project.Path, events.Reader, CancellationToken.None).Runs
        );
    }

    [Fact]
    public void Waiting_files_carry_the_stale_banner_while_watching()
    {
        using var h = new GraphHarness("""{"watch":"notify"}""");
        h.Write("a.rs", "fn alpha() {}\n");
        var state = State(h);
        state.Absorb(Ev(h, "a.rs"), T0);
        state.Publish();

        Assert.StartsWith(
            "stale: 1 files pending (a.rs)\n",
            GraphTools.Symbol(h.Cx, h.Project.Path, "alpha"),
            StringComparison.Ordinal
        );
        state.Settle(T0 + WatchState.Quiet);
        Assert.DoesNotContain(
            "stale:",
            GraphTools.Symbol(h.Cx, h.Project.Path, "alpha"),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Watching_is_off_by_default_and_never_starts_on_a_refused_root()
    {
        using var h = new GraphHarness();
        Assert.Null(GraphWatch.Start(h.Cx, h.Project.Path, CancellationToken.None));
        h.GraphJson = """{"watch":"notify"}""";
        Assert.Null(GraphWatch.Start(h.Cx, "/", CancellationToken.None));
    }

    // The one check through the OS watcher: a new file is indexed without a call walking.
    [Fact]
    public void The_watcher_indexes_a_new_file_and_drops_a_deleted_one()
    {
        using var h = new GraphHarness("""{"watch":"notify"}""");
        h.Write("lib.rs", "pub fn seed() {}\n");
        h.Run();
        using var stop = new CancellationTokenSource();
        var thread = GraphWatch.Start(h.Cx, h.Project.Path, stop.Token);
        Assert.NotNull(thread);
        try
        {
            // A write before the stream is live is lost: rewrite until the watcher sees one.
            var file = h.Project.Combine("watched.rs");
            Assert.True(
                Eventually(() =>
                {
                    File.WriteAllText(
                        file,
                        "pub fn watched() {}\n// " + Environment.TickCount64 + "\n"
                    );
                    return Defs(h, "watched").Count == 1;
                }),
                "the watcher never indexed watched.rs"
            );
            File.Delete(file);
            Assert.True(
                Eventually(() => Defs(h, "watched").Count == 0),
                "watched.rs kept its rows"
            );
        }
        finally
        {
            stop.Cancel();
            thread.Join();
        }
    }

    private static bool Eventually(Func<bool> done)
    {
        var cap = Stopwatch.StartNew();
        while (cap.Elapsed < TimeSpan.FromSeconds(15))
        {
            if (done())
            {
                return true;
            }

            Thread.Sleep(WatchState.Quiet * 2);
        }

        return false;
    }
}
