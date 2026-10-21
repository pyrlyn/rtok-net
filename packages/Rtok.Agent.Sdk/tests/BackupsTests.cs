namespace Rtok.Agent.Sdk.Tests;

public sealed class BackupsTests : IDisposable
{
    private readonly Scratch dir = new();

    public void Dispose() => dir.Dispose();

    [Fact]
    public void NoFileNoCopy() => Assert.Null(Backups.Take(dir["missing.json"], 5));

    [Fact]
    public void CopyGoesIntoTheBackupFolderWithTheSecond()
    {
        var path = dir.Write("mcp.json", "{}");
        var bak = Backups.Take(path, 5, new FixedTime(1_700_000_000));
        Assert.Equal(dir["_backup/mcp.json.bak-1700000000"], bak);
        Assert.Equal("{}", dir.Read("_backup/mcp.json.bak-1700000000"));
    }

    [Fact]
    public void SameContentIsCopiedOnce()
    {
        var path = dir.Write("mcp.json", "{}");
        Assert.NotNull(Backups.CopyAt(path, 100));
        Assert.Null(Backups.CopyAt(path, 200));
        File.WriteAllText(path, "{\"a\":1}");
        Assert.NotNull(Backups.CopyAt(path, 300));
        Assert.Equal(["mcp.json.bak-100", "mcp.json.bak-300"], dir.Entries("_backup"));
    }

    [Fact]
    public void AnotherFilesCopyNeverCountsAsThisFilesUndo()
    {
        var a = dir.Write("a.json", "same");
        var b = dir.Write("b.json", "same");
        Assert.NotNull(Backups.CopyAt(a, 100));
        Assert.NotNull(Backups.CopyAt(b, 100));
        Assert.Equal(["a.json.bak-100", "b.json.bak-100"], dir.Entries("_backup"));
    }

    [Fact]
    public void SameSecondCopiesAreNumberedPastTheHighest()
    {
        var path = dir.Write("mcp.json", "1");
        Backups.CopyAt(path, 100);
        File.WriteAllText(path, "2");
        Backups.CopyAt(path, 100);
        // A slot pruning freed is never reused: the new copy must sort newest.
        File.Delete(dir["_backup/mcp.json.bak-100"]);
        File.WriteAllText(path, "3");
        Assert.Equal(dir["_backup/mcp.json.bak-100-2"], Backups.CopyAt(path, 100));
    }

    [Fact]
    public void CopyNeverOverwritesAForeignFileInTheSlot()
    {
        var path = dir.Write("mcp.json", "new");
        dir.Write("_backup/mcp.json.bak-100-x", "not a generation");
        Directory.CreateDirectory(dir["_backup/mcp.json.bak-100"]);
        var bak = Backups.CopyAt(path, 100);
        Assert.Equal(dir["_backup/mcp.json.bak-100-1"], bak);
        Assert.Equal("not a generation", dir.Read("_backup/mcp.json.bak-100-x"));
    }

    [Fact]
    public void CapKeepsTheNewestGenerationsPerFile()
    {
        var path = dir.Write("mcp.json");
        var other = dir.Write("other.json", "o");
        Backups.CopyAt(other, 1);
        for (var ts = 1; ts <= 4; ts++)
        {
            File.WriteAllText(path, $"v{ts}");
            Backups.Take(path, 2, new FixedTime(ts));
        }
        Assert.Equal(
            ["mcp.json.bak-3", "mcp.json.bak-4", "other.json.bak-1"],
            dir.Entries("_backup")
        );
    }

    [Fact]
    public void PruneNeverDeletesTheKeptCopy()
    {
        var path = dir.Write("mcp.json", "old");
        Backups.CopyAt(path, 900);
        File.WriteAllText(path, "new");
        // An older clock still names the copy that was just taken; it stays, the rest go.
        var kept = Backups.CopyAt(path, 100)!;
        Backups.Prune(kept, 1);
        Assert.Equal(["mcp.json.bak-100"], dir.Entries("_backup"));
    }

    [Fact]
    public void ZeroKeepsAll()
    {
        var path = dir.Write("mcp.json");
        for (var ts = 1; ts <= 7; ts++)
        {
            File.WriteAllText(path, $"v{ts}");
            Backups.Take(path, 0, new FixedTime(ts));
        }
        Assert.Equal(7, dir.Entries("_backup").Length);
        Assert.Empty(Backups.Stale(dir["_backup"], 0));
    }

    [Fact]
    public void OnlyGenerationsInABackupFolderArePruned()
    {
        dir.Write("_backup/mcp.json.bak-1", "a");
        dir.Write("_backup/mcp.json.bak-x", "b");
        dir.Write("_backup/mcp.json.bak-2-", "c");
        dir.Write("_backup/mcp.json.bak", "d");
        dir.Write("_backup/mcp.json.bak-3", "e");
        Backups.Prune(dir["_backup/mcp.json.bak-3"], 1);
        Assert.Equal(
            ["mcp.json.bak", "mcp.json.bak-2-", "mcp.json.bak-3", "mcp.json.bak-x"],
            dir.Entries("_backup")
        );

        dir.Write("elsewhere/mcp.json.bak-1", "a");
        dir.Write("elsewhere/mcp.json.bak-2", "b");
        Backups.Prune(dir["elsewhere/mcp.json.bak-2"], 1);
        Assert.Equal(["mcp.json.bak-1", "mcp.json.bak-2"], dir.Entries("elsewhere"));
    }

    [Fact]
    public void StaleListsWhatALoweredCapHasNoRoomFor()
    {
        foreach (
            var f in new[] { "a.json.bak-1", "a.json.bak-2", "a.json.bak-2-1", "b.json.bak-5" }
        )
        {
            dir.Write($"_backup/{f}", f);
        }
        Assert.Equal(
            [dir["_backup/a.json.bak-1"], dir["_backup/a.json.bak-2"]],
            Backups.Stale(dir["_backup"], 1)
        );
        Assert.Empty(Backups.Stale(dir["_backup"], 3));
        Assert.Empty(Backups.Stale(dir.Root, 1));
    }

    [Fact]
    public void ANoChangeRunDeletesItsOwnCopiesAndKeepsOlderOnes()
    {
        var path = dir.Write("mcp.json", "v1");
        Backups.CopyAt(path, 1);
        File.WriteAllText(path, "v2");
        var run = new BackupRun(1, new FixedTime(2));
        Assert.NotNull(run.Take(path));
        run.Finish(changed: false);
        Assert.Equal(["mcp.json.bak-1"], dir.Entries("_backup"));
    }

    [Fact]
    public void AChangedRunPrunesToTheCap()
    {
        var path = dir.Write("mcp.json", "v1");
        Backups.CopyAt(path, 1);
        File.WriteAllText(path, "v2");
        var run = new BackupRun(1, new FixedTime(2));
        run.Take(path);
        run.Finish(changed: true);
        Assert.Equal(["mcp.json.bak-2"], dir.Entries("_backup"));
        Assert.Empty(run.Copies);
    }

    private sealed class FixedTime(long seconds) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(seconds);
    }
}
