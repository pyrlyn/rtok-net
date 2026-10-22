using Pyrlyn.FsKit;
using Rtok.Agents.Junk;

namespace Rtok.Agents.Tests;

/// <summary>The check right before removal: whatever the plan said, these stay.</summary>
public sealed class JunkRecheckTests : IDisposable
{
    private readonly JunkHome home = new();

    public void Dispose() => home.Dispose();

    private (string Why, bool Failed)? Recheck(
        string path,
        string evidence = JunkEvidence.Section22,
        params string[] keep
    ) => Check(path, JunkTree.Real(path), evidence, keep);

    private (string Why, bool Failed)? Check(
        string path,
        string real,
        string evidence,
        string[] keep
    ) =>
        JunkClear.Recheck(
            new JunkPlanned
            {
                Agent = "claude",
                Kind = "cache",
                Path = path,
                Bytes = 0,
                Reason = "",
                Evidence = evidence,
                Real = real,
            },
            keep,
            home.Now
        );

    [Fact]
    public void An_old_folder_passes()
    {
        home.File("c/a");

        Assert.Null(Recheck(home["c"]));
    }

    [Fact]
    public void A_path_already_gone_is_no_failure() =>
        Assert.Equal(("already gone", false), Recheck(home["missing"]));

    [Fact]
    public void A_path_whose_real_place_moved_stays()
    {
        home.File("c/a");

        Assert.Equal(
            ("changed since plan: its real path moved", true),
            Check(home["c"], home["elsewhere"], JunkEvidence.Section22, [])
        );
    }

    [Fact]
    public void A_symlink_is_never_followed()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "a symlink needs developer mode");
        home.File("real/a");
        File.CreateSymbolicLink(home["link"], home["real"]);

        Assert.Equal(("a symlink: never followed", true), Recheck(home["link"]));
    }

    [Fact]
    public void A_folder_holding_a_protected_file_stays()
    {
        var settings = home.File("c/settings.json");

        Assert.Equal(
            ("is or holds a protected file (store, settings, lockfile)", true),
            Recheck(home["c"], JunkEvidence.Section22, settings)
        );
    }

    [Fact]
    public void A_lockfile_that_appeared_keeps_the_folder()
    {
        home.File("c/deep/Cargo.lock");

        Assert.Equal(
            ("holds a package-manager lockfile: " + Path.Combine("deep", "Cargo.lock"), true),
            Recheck(home["c"])
        );
    }

    [Fact]
    public void A_tag_that_went_away_keeps_the_folder()
    {
        var dir = home.Tagged("c");
        File.Delete(Path.Combine(dir, CacheDirTag.FileName));

        Assert.Equal(("no longer a tagged cache", true), Recheck(dir, JunkEvidence.Tag));
    }

    [Fact]
    public void A_write_in_the_last_minute_keeps_it()
    {
        home.File("c/a", home.Now - TimeSpan.FromSeconds(5));

        Assert.Equal(("modified in the last minute", true), Recheck(home["c"]));
    }

    [Fact]
    public void Protected_holds_the_store_the_markers_and_the_credentials()
    {
        var kept = JunkClear.Protected(home.Config, home.Roots, [home[".grok/mcp.json"]]);

        Assert.Contains(FsPath.Resolved(home[".rtok/rtok.db-wal"]), kept, StringComparer.Ordinal);
        Assert.Contains(FsPath.Resolved(home[".grok/mcp.json"]), kept, StringComparer.Ordinal);
        Assert.Contains(
            FsPath.Resolved(home[".claude/.credentials.json"]),
            kept,
            StringComparer.Ordinal
        );
        Assert.Equal(kept.Count, kept.Distinct(StringComparer.Ordinal).Count());
    }
}
