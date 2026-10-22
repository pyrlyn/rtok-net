using System.Text.Json;
using Pyrlyn.FsKit;
using Rtok.Agents.Junk;

namespace Rtok.Agents.Tests;

public sealed class JunkClearTests : IDisposable
{
    private static readonly JunkFilter Cache = new() { Kinds = ["cache"] };

    private readonly JunkHome home = new();

    public void Dispose() => home.Dispose();

    [Fact]
    public void Without_a_flag_only_rtoks_own_old_logs_go()
    {
        var past = home.File(".rtok/logs/rtok.log.7");
        home.File(".rtok/logs/rtok.log.2");
        home.File(".claude/paste-cache/a");

        var cleared = home.Clear(new(), false);

        var item = Assert.Single(cleared.Items);
        Assert.Equal(("rtok", "log", past), (item.Agent, item.Kind, item.Path));
        Assert.Equal("past `[log] files` = 5", item.Reason);
        Assert.True(File.Exists(past));
        Assert.EndsWith(
            "dry run: 1 items, 4.9 KB to free, nothing changed; rerun with --yes\n",
            JunkClear.Text(cleared, home.View),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void An_empty_home_has_nothing_to_clear()
    {
        var cleared = home.Clear(Cache, true);

        Assert.Empty(cleared.Items);
        Assert.Equal("Nothing to clear\n", JunkClear.Text(cleared, home.View));
        Assert.False(cleared.Failed);
    }

    [Fact]
    public void A_dry_run_changes_nothing()
    {
        var file = home.File(".claude/paste-cache/a");

        var cleared = home.Clear(Cache, false);

        Assert.True(Assert.Single(cleared.Items).Planned);
        Assert.Equal(1, cleared.Items[0].Files);
        Assert.True(File.Exists(file));
        Assert.Contains(
            "rerun with --yes",
            JunkClear.Text(cleared, home.View),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Clearing_a_cache_keeps_its_folder_and_tag()
    {
        home.File(".claude/paste-cache/a");
        var tagged = home.Tagged(".claude/plugins/old");

        var cleared = home.Clear(Cache, true);

        Assert.All(cleared.Items, p => Assert.Equal("removed", p.Note));
        Assert.Empty(Directory.EnumerateFileSystemEntries(home[".claude/paste-cache"]));
        Assert.Equal(
            [Path.Combine(tagged, CacheDirTag.FileName)],
            Directory.GetFileSystemEntries(tagged)
        );
        Assert.Equal(cleared.PlannedBytes, cleared.FreedBytes);
        Assert.StartsWith(
            "Freed ",
            JunkClear.Text(cleared, home.View).Split('\n')[^2],
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Only_named_kinds_clear_and_documented_items_only()
    {
        home.File(".claude/paste-cache/a");
        home.File(".claude/todos/x.lock");
        home.File(".claude/projects/p/0b8e6c1e-1111-4a5b-9c7d-0123456789ab.jsonl");

        Assert.DoesNotContain(
            home.Clear(new() { Kinds = ["locks"] }, false).Items,
            p => p.Kind is "locks"
        );
        Assert.DoesNotContain(home.Clear(Cache, false).Items, p => p.Kind is "sessions");
        Assert.Single(home.Clear(new() { Kinds = ["sessions"] }, false).Items);
    }

    [Fact]
    public void Every_flag_leaves_credentials_settings_sessions_and_the_store()
    {
        string[] kept =
        [
            home.File(".claude/settings.json", body: "{}"),
            home.File(".claude/.credentials.json", body: "{}"),
            home.File(".claude.json", body: "{}"),
            home.File(".claude/plugins/p/plugin.json", body: "{}"),
            home.File(".claude/projects/p/0b8e6c1e-1111-4a5b-9c7d-0123456789ab.jsonl"),
            home.File(".rtok/rtok.db"),
            home.File(".rtok/rtok.db-wal"),
            home.File(".rtok/rtok.db-shm"),
        ];
        home.File(".claude/paste-cache/a");
        home.File(".claude/shell-snapshots/s.sh");
        home.File(".claude/debug/d.txt");

        var safe = home.Clear(new() { Agents = ["claude"] }, true);
        var everything = home.Clear(
            new()
            {
                IncludeReview = true,
                Kinds = [.. JunkFilter.KindNames.Where(k => k is not "sessions")],
            },
            true
        );

        Assert.Equal(
            ["cache", "temp"],
            safe.Items.Select(p => p.Kind).Order(StringComparer.Ordinal),
            StringComparer.Ordinal
        );
        Assert.Equal(["logs"], everything.Items.Select(p => p.Kind), StringComparer.Ordinal);
        Assert.All(kept, k => Assert.True(File.Exists(k), k));
    }

    [Fact]
    public void A_running_agent_keeps_its_live_paths()
    {
        home.File(".claude/paste-cache/a");
        home.File(".claude/shell-snapshots/s.sh");
        var asked = new List<string>();

        var cleared = home.Clear(
            new() { Agents = ["claude"] },
            true,
            running: id =>
            {
                asked.Add(id);
                return true;
            }
        );

        Assert.Equal(2, cleared.Items.Count);
        Assert.All(
            cleared.Items,
            item =>
                Assert.Equal(
                    (false, "skip", JunkClear.Running),
                    (item.Planned, item.Action, item.Note)
                )
        );
        Assert.Equal(["claude"], asked);
        Assert.True(File.Exists(home[".claude/paste-cache/a"]));
    }

    [Fact]
    public void Older_than_cuts_by_the_newest_file()
    {
        home.File(".claude/paste-cache/a");

        Assert.Empty(home.Clear(Cache with { OlderThan = TimeSpan.FromDays(60) }, false).Items);
        Assert.Single(home.Clear(Cache with { OlderThan = TimeSpan.FromDays(7) }, false).Items);
    }

    [Fact]
    public void A_folder_holding_a_lockfile_is_not_planned()
    {
        home.File(".claude/paste-cache/a");
        home.File(".claude/paste-cache/proj/yarn.lock");

        var item = Assert.Single(home.Clear(Cache, false).Items);

        Assert.False(item.Planned);
        Assert.Equal(
            "holds a package-manager lockfile: " + Path.Combine("proj", "yarn.lock"),
            item.Note
        );
    }

    [Fact]
    public void Trash_moves_each_entry_and_says_so()
    {
        var file = home.File(".claude/paste-cache/a");
        var moved = new List<string>();

        var cleared = home.Clear(Cache with { Trash = true }, true, trash: moved.Add);

        Assert.Equal("moved to trash", Assert.Single(cleared.Items).Note);
        Assert.Equal([file], moved);
    }

    [Fact]
    public void A_failed_removal_is_kept_and_fails_the_run()
    {
        home.File(".claude/paste-cache/a");

        var cleared = home.Clear(
            Cache with
            {
                Trash = true,
            },
            true,
            trash: _ => throw new IOException("the trash is full")
        );

        var item = Assert.Single(cleared.Items);
        Assert.True(item.Failed);
        Assert.Equal("failed, kept: the trash is full", item.Note);
        Assert.True(cleared.Failed);
        Assert.Equal(0, cleared.FreedBytes);
    }

    [Fact]
    public void Archives_go_through_the_stores_retention()
    {
        var archive = home.File(".rtok/archive/a.zst");
        var store = new FakeJunkStore(archive);
        home.Store = store;

        var cleared = home.Clear(new(), true);

        var item = Assert.Single(cleared.Items);
        Assert.Equal(("archive", "removed"), (item.Kind, item.Note));
        Assert.Equal([(30, 3)], store.Runs);
    }

    [Fact]
    public void A_retention_that_fails_keeps_the_archives()
    {
        var archive = home.File(".rtok/archive/a.zst");
        home.Store = new FakeJunkStore(archive) { Fails = new InvalidOperationException("locked") };

        var cleared = home.Clear(new(), true);

        Assert.Equal("failed, kept: locked", Assert.Single(cleared.Items).Note);
        Assert.True(cleared.Failed);
    }

    [Theory]
    [InlineData(30u, null, 30u)]
    [InlineData(30u, 7.0, 30u)]
    [InlineData(30u, 45.0, 45u)]
    [InlineData(30u, 1.5, 30u)]
    [InlineData(10u, 10.5, 11u)]
    [InlineData(0u, 90.0, 0u)]
    public void Older_than_only_widens_archive_retention(
        uint days,
        double? olderThan,
        uint expected
    )
    {
        home.Config.Core.RetainCallsDays = days;
        var filter = new JunkFilter
        {
            OlderThan = olderThan is { } d ? TimeSpan.FromDays(d) : null,
        };

        Assert.Equal(expected, JunkClear.RetentionDays(home.Config, filter));
    }

    [Fact]
    public void The_json_names_every_item()
    {
        home.File(".claude/paste-cache/a");

        using var doc = JsonDocument.Parse(JunkClear.Json(home.Clear(Cache, false)));

        var root = doc.RootElement;
        Assert.False(root.GetProperty("yes").GetBoolean());
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal("claude", item.GetProperty("agent").GetString());
        Assert.Equal("clear", item.GetProperty("action").GetString());
        Assert.Equal(JsonValueKind.Number, item.GetProperty("last_used").ValueKind);
        Assert.Equal(
            root.GetProperty("planned_bytes").GetInt64(),
            item.GetProperty("bytes").GetInt64()
        );
    }
}
