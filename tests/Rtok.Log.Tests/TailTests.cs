using Rtok.Testing;

namespace Rtok.Log.Tests;

/// <summary><c>rtok logs</c>: the last lines newest first across <c>.N</c>, and how they show.</summary>
public sealed class TailTests : IDisposable
{
    private readonly TempDir _dir = new("rtok-tail-");

    private string LogPath => _dir.Combine("rtok.log");

    [Fact]
    public void NothingLoggedIsEmpty() => Assert.Empty(LogTail.Read(LogPath, 10));

    [Fact]
    public void LinesComeNewestFirstAcrossTheRotatedFiles()
    {
        File.WriteAllText(LogPath, "live-3\nlive-4\n");
        File.WriteAllText(LogPath + ".1", "old-1\nold-2\n");
        File.WriteAllText(LogPath + ".2", "oldest\n");

        Assert.Equal(["live-4", "live-3", "old-2", "old-1", "oldest"], LogTail.Read(LogPath, 10));
        Assert.Equal(["live-4", "live-3", "old-2"], LogTail.Read(LogPath, 3));
    }

    [Fact]
    public void TheWalkStopsAtTheFirstMissingFile()
    {
        File.WriteAllText(LogPath, "live\n");
        File.WriteAllText(LogPath + ".2", "orphan\n");

        Assert.Equal(["live"], LogTail.Read(LogPath, 10));
    }

    [Fact]
    public void ALineStillBeingWrittenIsLeftOut()
    {
        File.WriteAllText(LogPath, "whole\nhalf");

        Assert.Equal(["whole"], LogTail.Read(LogPath, 10));
    }

    [Fact]
    public void RowsAreNumberedFromOneNewest() =>
        Assert.Equal(["1 b", "2 a"], LogScreen.Numbered(["b", "a"], colour: false));

    [Theory]
    [InlineData("error", "31")]
    [InlineData("warn", "33")]
    [InlineData("debug", "2")]
    public void TheLevelIsColoured(string level, string code) =>
        Assert.Equal(
            $"2026-10-22 10:00:00 \u001b[{code}m{level}\u001b[0m cli/x: m",
            LogScreen.Colour($"2026-10-22 10:00:00 {level} cli/x: m")
        );

    [Theory]
    [InlineData("2026-10-22 10:00:00 info cli/x: m")]
    [InlineData("not a log line")]
    public void InfoAndOtherShapesStayPlain(string line) =>
        Assert.Equal(line, LogScreen.Colour(line));

    public void Dispose() => _dir.Dispose();
}
