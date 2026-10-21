using Rtok.Testing;

namespace Rtok.Log.Tests;

/// <summary>
/// <see cref="LogFollow"/>: new lines once each, across rotations found by file identity, and
/// from the start again after a truncation in place.
/// </summary>
public sealed class FollowTests : IDisposable
{
    private readonly TempDir _dir = new("rtok-follow-");

    private string LogPath => _dir.Combine("rtok.log");

    [Fact]
    public void PrimingReturnsTheLiveLinesAndPollTheNewOnes()
    {
        File.WriteAllText(LogPath, "a\nb\n");
        var (follow, lines) = LogFollow.Primed(LogPath);
        using (follow)
        {
            Assert.Equal(["a", "b"], lines);
            Assert.Empty(follow.Poll(LogPath));

            File.AppendAllText(LogPath, "c\nhal");
            Assert.Equal(["c"], follow.Poll(LogPath));
            File.AppendAllText(LogPath, "f\n");
            Assert.Equal(["half"], follow.Poll(LogPath));
        }
    }

    [Fact]
    public void ARotationGivesTheOldFilesRestThenTheNewFile()
    {
        File.WriteAllText(LogPath, "a\n");
        var (follow, _) = LogFollow.Primed(LogPath);
        using (follow)
        {
            File.AppendAllText(LogPath, "b\n");
            Rotate();
            File.WriteAllText(LogPath, "c\n");

            Assert.Equal(["b", "c"], follow.Poll(LogPath));
            File.AppendAllText(LogPath, "d\n");
            Assert.Equal(["d"], follow.Poll(LogPath));
        }
    }

    [Fact]
    public void TwoFilesWithTheSameFirstLineAreStillTwoFiles()
    {
        File.WriteAllText(LogPath, "same\n");
        var (follow, _) = LogFollow.Primed(LogPath);
        using (follow)
        {
            Rotate();
            File.WriteAllText(LogPath, "same\nnext\n");

            Assert.Equal(["same", "next"], follow.Poll(LogPath));
        }
    }

    [Fact]
    public void TheRestOfAFileRetentionDeletedStillComesThroughTheHandle()
    {
        File.WriteAllText(LogPath, "a\n");
        var (follow, _) = LogFollow.Primed(LogPath);
        using (follow)
        {
            File.AppendAllText(LogPath, "b\n");
            Rotate();
            File.Delete(LogPath + ".1");
            File.WriteAllText(LogPath, "c\n");

            Assert.Equal(["b", "c"], follow.Poll(LogPath));
        }
    }

    [Fact]
    public void SeveralRotationsBetweenPollsLoseNoGeneration()
    {
        File.WriteAllText(LogPath, "a\n");
        var (follow, _) = LogFollow.Primed(LogPath);
        using (follow)
        {
            File.AppendAllText(LogPath, "b\n");
            Rotate();
            File.WriteAllText(LogPath, "c\n");
            Rotate();
            File.WriteAllText(LogPath, "d\n");
            Rotate();
            File.WriteAllText(LogPath, "e\n");

            Assert.Equal(["b", "c", "d", "e"], follow.Poll(LogPath));
        }
    }

    [Fact]
    public void ATruncationInPlaceStartsAgainFromTheTop()
    {
        File.WriteAllText(LogPath, "a\nb\nc\n");
        var (follow, _) = LogFollow.Primed(LogPath);
        using (follow)
        {
            using (var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Write))
            {
                stream.SetLength(0);
            }
            File.AppendAllText(LogPath, "x\n");

            Assert.Equal(["x"], follow.Poll(LogPath));
        }
    }

    [Fact]
    public void AnEmptyReplacementIsFollowedFromItsFirstLine()
    {
        File.WriteAllText(LogPath, "a\n");
        var (follow, _) = LogFollow.Primed(LogPath);
        using (follow)
        {
            Rotate();
            File.WriteAllText(LogPath, "");
            Assert.Empty(follow.Poll(LogPath));

            File.AppendAllText(LogPath, "b\n");
            Assert.Equal(["b"], follow.Poll(LogPath));
        }
    }

    [Fact]
    public void AMissingPathReadsAsNothingNewUntilItAppears()
    {
        var (follow, lines) = LogFollow.Primed(LogPath);
        using (follow)
        {
            Assert.Empty(lines);
            Assert.Empty(follow.Poll(LogPath));

            File.WriteAllText(LogPath, "first\n");
            Assert.Equal(["first"], follow.Poll(LogPath));

            Rotate();
            Assert.Empty(follow.Poll(LogPath));
        }
    }

    public void Dispose() => _dir.Dispose();

    // What Pyrlyn.RotatingLog does: shift every .N up one, then the live file to .1.
    private void Rotate()
    {
        var n = 1;
        while (File.Exists($"{LogPath}.{n}"))
        {
            n++;
        }
        for (; n > 1; n--)
        {
            File.Move($"{LogPath}.{n - 1}", $"{LogPath}.{n}");
        }
        File.Move(LogPath, LogPath + ".1");
    }
}
