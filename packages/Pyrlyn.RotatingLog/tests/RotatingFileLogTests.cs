namespace Pyrlyn.RotatingLog.Tests;

public sealed class RotatingFileLogTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pyrlyn-log-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string LogPath => Path.Combine(_dir, "app.log");

    private RotatingFileLog Log(long maxBytes = 1 << 20, int files = 1, string level = "debug") =>
        new(
            new RotatingFileLogOptions
            {
                Path = LogPath,
                MaxBytes = maxBytes,
                Files = files,
                Level = level,
            }
        );

    private string[] FileNames() =>
        [.. Directory.GetFiles(_dir).Select(Path.GetFileName).Order(StringComparer.Ordinal)!];

    [Theory]
    [InlineData(0, "1970-01-01 00:00:00")]
    [InlineData(1_788_966_245, "2026-09-09 15:04:05")]
    [InlineData(1_709_209_845, "2024-02-29 12:30:45")]
    [InlineData(4_107_542_400, "2100-03-01 00:00:00")]
    public void StampsACivilUtcDate(long seconds, string stamp)
    {
        var line = RotatingFileLog.FormatLine(
            DateTimeOffset.FromUnixTimeSeconds(seconds).ToOffset(TimeSpan.FromHours(3)),
            "info",
            "src",
            "ev",
            "m"
        );
        Assert.Equal($"{stamp} info src/ev: m", line);
    }

    [Fact]
    public void TheLevelIsAFloorAndAnUnknownLevelAlwaysPasses()
    {
        Assert.True(RotatingFileLog.IsEnabled("warn", "error"));
        Assert.True(RotatingFileLog.IsEnabled("warn", "WARN"));
        Assert.False(RotatingFileLog.IsEnabled("warn", "info"));
        Assert.False(RotatingFileLog.IsEnabled("warn", "debug"));
        Assert.True(RotatingFileLog.IsEnabled("warn", "PANIC"));
        Assert.False(RotatingFileLog.IsEnabled("nonsense", "warn"));
    }

    [Fact]
    public void ALineBelowTheLevelTouchesNoDisk()
    {
        var nested = Path.Combine(_dir, "logs", "app.log");
        var log = new RotatingFileLog(new RotatingFileLogOptions { Path = nested, Level = "warn" });
        log.Write("debug", "test", "quiet", "nothing");
        log.Write("info", "test", "quiet", "nothing");
        Assert.False(Directory.Exists(Path.GetDirectoryName(nested)));
    }

    [Fact]
    public void WritesOneStampedLineAndCreatesTheDirectory()
    {
        var path = Path.Combine(_dir, "deep", "logs", "app.log");
        var time = new FixedTime(DateTimeOffset.FromUnixTimeSeconds(1_788_966_245));
        var log = new RotatingFileLog(new RotatingFileLogOptions { Path = path, Time = time });
        log.Write("warn", "test", "multi", "first\nsecond\r\nthird");
        Assert.Equal(
            "2026-09-09 15:04:05 warn test/multi: first second  third\n",
            File.ReadAllText(path)
        );
    }

    [Fact]
    public void ErrorsAreCopiedAndWarningsStayInTheMainFile()
    {
        var log = Log();
        log.Write("warn", "app", "boot", "careful");
        log.Write("info", "app", "boot", "note");
        log.Write("ERROR", "app", "boot", "broken");
        log.Write("panic", "app", "boot", "unknown");
        var main = File.ReadAllText(LogPath);
        var errors = File.ReadAllText(Path.Combine(_dir, "errors.log"));
        Assert.Equal(Path.Combine(_dir, "errors.log"), log.ErrorsPath);
        Assert.Contains("careful", main, StringComparison.Ordinal);
        Assert.Contains("note", main, StringComparison.Ordinal);
        Assert.Contains("broken", main, StringComparison.Ordinal);
        Assert.Single(errors.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("ERROR app/boot: broken", errors, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutTheCopyAnErrorGoesOnlyToTheMainFile()
    {
        var quiet = new RotatingFileLog(
            new RotatingFileLogOptions { Path = LogPath, CopyErrors = false }
        );
        quiet.Write("error", "app", "boot", "broken");
        Assert.Null(quiet.ErrorsPath);
        Assert.Equal(["app.log"], FileNames());
    }

    [Fact]
    public void RotationKeepsFilesPlusTheLiveOneAndTheNewestLineIsLive()
    {
        var log = Log(maxBytes: 200, files: 2);
        for (var i = 0; i < 50; i++)
        {
            log.Write("info", "test", "rotate", $"line {i}");
        }
        Assert.Equal(["app.log", "app.log.1", "app.log.2"], FileNames());
        var live = File.ReadAllText(LogPath);
        Assert.Contains("line 49", live, StringComparison.Ordinal);
        Assert.True(live.Length <= 200, $"the live file is bounded: {live.Length}");
        var older = File.ReadAllText(LogPath + ".1");
        Assert.True(older.Length <= 200, $"a rotated file is bounded: {older.Length}");
    }

    [Fact]
    public void ZeroFilesTruncatesTheLiveFileAndKeepsNone()
    {
        var log = Log(maxBytes: 100, files: 0);
        for (var i = 0; i < 20; i++)
        {
            log.Write("info", "test", "rotate", $"line {i}");
        }
        Assert.Equal(["app.log"], FileNames());
        Assert.Contains("line 19", File.ReadAllText(LogPath), StringComparison.Ordinal);
    }

    [Fact]
    public void ALineLongerThanTheBoundIsStillWritten()
    {
        var log = Log(maxBytes: 10, files: 1);
        log.Write("info", "test", "long", new string('x', 50));
        Assert.Contains(new string('x', 50), File.ReadAllText(LogPath), StringComparison.Ordinal);
    }

    [Fact]
    public void ARotationDecidedOnAStaleSizeIsDroppedUnderTheLock()
    {
        var log = Log(maxBytes: 100, files: 3);
        File.WriteAllText(LogPath + ".1", "history\n");
        File.WriteAllText(LogPath, "fresh\n");
        log.RotateIfOver(LogPath, 10);
        Assert.Equal("history\n", File.ReadAllText(LogPath + ".1"));
        Assert.False(File.Exists(LogPath + ".2"));

        File.WriteAllText(LogPath, new string('x', 99));
        log.RotateIfOver(LogPath, 10);
        Assert.Equal("history\n", File.ReadAllText(LogPath + ".2"));
        Assert.False(File.Exists(LogPath));
    }

    [Fact]
    public void AnUnwritableLogFailsOpen()
    {
        // The log's directory is a file: every write fails, none throws.
        var blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "");
        var log = new RotatingFileLog(
            new RotatingFileLogOptions { Path = Path.Combine(blocker, "app.log") }
        );
        log.Write("error", "test", "open", "nowhere");
    }

    [Fact]
    public void RotatedPathFollowsTheExtension()
    {
        Assert.Equal("/x/rtok.log.3", RotatingFileLog.RotatedPath("/x/rtok.log", 3));
    }

    [Fact]
    public async Task WriterProcessesLoseNoLineAcrossRotations()
    {
        // About 6 KB per writer at 1 KB per file: dozens of rotations, all kept.
        const int Writers = 6;
        const int Lines = 150;
        var writers = Enumerable
            .Range(0, Writers)
            .Select(w => WriterProcess.Start(LogPath, 1024, 200, $"w{w}", Lines))
            .ToList();
        foreach (var p in writers)
        {
            await p.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0, p.ExitCode);
            p.Dispose();
        }

        var files = FileNames()
            .Where(n => n.StartsWith("app.log", StringComparison.Ordinal))
            .ToList();
        Assert.True(files.Count > 10, "the writers actually rotated");
        var messages = files
            .SelectMany(n => File.ReadAllLines(Path.Combine(_dir, n)))
            .Select(l => l[(l.IndexOf(": ", StringComparison.Ordinal) + 2)..])
            .ToList();
        var expected = Enumerable
            .Range(0, Writers)
            .SelectMany(w => Enumerable.Range(0, Lines).Select(i => $"w{w}:{i}"));
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            messages.Order(StringComparer.Ordinal)
        );
        foreach (var n in files)
        {
            Assert.True(new FileInfo(Path.Combine(_dir, n)).Length <= 1024, n);
        }
        var errors = FileNames()
            .Where(n => n.StartsWith("errors.log", StringComparison.Ordinal))
            .Sum(n => File.ReadAllLines(Path.Combine(_dir, n)).Length);
        Assert.Equal(Writers * Lines / 10, errors);
    }

    [Fact]
    public async Task WriterProcessesNeverLeaveMoreThanTheConfiguredFiles()
    {
        const int Writers = 6;
        var writers = Enumerable
            .Range(0, Writers)
            .Select(w => WriterProcess.Start(LogPath, 300, 2, $"w{w}", 100))
            .ToList();
        foreach (var p in writers)
        {
            await p.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0, p.ExitCode);
            p.Dispose();
        }

        Assert.Equal(
            ["app.log", "app.log.1", "app.log.2", "errors.log", "errors.log.1", "errors.log.2"],
            FileNames()
        );
        // Every kept line is whole: no two writers interleaved inside a line.
        foreach (var name in FileNames())
        {
            foreach (var line in File.ReadAllLines(Path.Combine(_dir, name)))
            {
                Assert.Matches(
                    @"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d (info|error) writer/w\d: w\d:\d+$",
                    line
                );
            }
        }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
