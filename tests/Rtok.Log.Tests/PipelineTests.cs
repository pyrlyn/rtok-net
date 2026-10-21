using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rtok.Config;
using Rtok.Store;
using Rtok.Store.Model;
using Rtok.Testing;
using Serilog.Events;

namespace Rtok.Log.Tests;

/// <summary>The one pipeline: its floor, the file line, <c>errors.log</c>, <c>to_db</c>, <c>RTOK_LOG</c>.</summary>
public sealed class PipelineTests : IDisposable
{
    private readonly TempDir _dir = new("rtok-log-");

    private string LogPath => _dir.Combine("logs", "rtok.log");

    private string DbPath => _dir.Combine("rtok.db");

    [Fact]
    public void ALineHasLevelSourceNameAndMessage()
    {
        using (var log = Create())
        {
            log.ForSource("hook").Named("pre").Information("rewrote {Count} calls", 3);
        }

        var line = Assert.Single(Lines());
        Assert.EndsWith(" info hook/pre: rewrote 3 calls", line, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyLogSectionLogsToTheHomesRotatingFileAtInfo()
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["RTOK_HOME"] = _dir.Path,
            ["HOME"] = _dir.Path,
            ["USERPROFILE"] = _dir.Path,
        };
        var section = ConfigLoader
            .Load(new ConfigLoadOptions { Environment = env, WorkingDirectory = _dir.Path })
            .Config.Log;

        Assert.Equal(LogPath, section.Path);
        Assert.Equal(
            (1048576UL, 5U, "info", true),
            (section.MaxBytes, section.Files, section.Level, section.ToDb)
        );
        using (var log = RtokLog.Create(section, new RtokLogOptions { Debug = "off" }))
        {
            Assert.False(log.IsEnabled(LogEventLevel.Debug));
            log.ForSource("cli").Named("x").Information("home");
        }
        Assert.Single(Lines());
    }

    [Fact]
    public void RotationAtMaxBytesKeepsFiveFilesAndDropsTheOldest()
    {
        var section = Section(toDb: false);
        section.MaxBytes = 1024;
        using (var log = RtokLog.Create(section, new RtokLogOptions { Debug = "off" }))
        {
            for (var i = 0; i < 400; i++)
            {
                log.ForSource("cli").Named("x").Information("line {I:D3}", i);
            }
        }

        var files = Directory.GetFiles(_dir.Combine("logs"), "rtok.log*");
        Assert.Equal(6, files.Length); // the live file and .1 to .5
        Assert.True(File.Exists(LogPath + ".5"));
        Assert.EndsWith("line 399", Lines()[^1], StringComparison.Ordinal);
        var kept = files.SelectMany(File.ReadAllLines).ToList();
        Assert.DoesNotContain(kept, l => l.EndsWith("line 000", StringComparison.Ordinal));
        Assert.All(files, f => Assert.True(new FileInfo(f).Length <= 1024, f));
    }

    [Fact]
    public void TheFloorDropsWhatIsBelowIt()
    {
        using (var log = Create(level: "warn"))
        {
            Assert.False(log.IsEnabled(LogEventLevel.Information));
            Assert.True(log.IsEnabled(LogEventLevel.Warning));
            log.ForSource("cli").Named("a").Information("dropped");
            log.ForSource("cli").Named("b").Warning("kept");
        }

        var line = Assert.Single(Lines());
        Assert.EndsWith(" warn cli/b: kept", line, StringComparison.Ordinal);
    }

    [Fact]
    public void AnErrorAlsoLandsInErrorsLog()
    {
        using (var log = Create())
        {
            log.ForSource("cli").Named("x").Error(new InvalidOperationException("boom"), "failed");
            log.ForSource("cli").Named("y").Information("fine");
        }

        var errors = File.ReadAllLines(_dir.Combine("logs", "errors.log"));
        var line = Assert.Single(errors);
        Assert.EndsWith(
            " error cli/x: failed: InvalidOperationException: boom",
            line,
            StringComparison.Ordinal
        );
        Assert.Equal(2, Lines().Count);
    }

    [Fact]
    public void ToDbWritesARowForRtoksOwnEventsOnly()
    {
        using (var log = Create(store: true))
        {
            log.ForSource("hook")
                .Named("pre")
                .ForContext(LogProperties.Session, "s1")
                .ForContext(LogProperties.Plugin, "cmd")
                .Information("row");
            using var factory = RtokLog.LoggerFactory(log);
            factory.CreateLogger("Some.Library").LogWarning("file only");
        }

        using var db = Read();
        var row = Assert.Single(db.Logs.ToList());
        Assert.Equal(
            ("info", "hook", "pre", "row"),
            (row.Level, row.SourceName, row.Name, row.Message)
        );
        Assert.Equal("s1", row.Session);
        Assert.Equal("cmd", row.Plugin);
        Assert.Equal(2, Lines().Count);
    }

    [Fact]
    public void ToDbOffNeverOpensTheStore()
    {
        var opened = false;
        using (
            var log = RtokLog.Create(
                Section(toDb: false),
                new RtokLogOptions
                {
                    Debug = "off",
                    OpenStore = () =>
                    {
                        opened = true;
                        throw new InvalidOperationException("never");
                    },
                }
            )
        )
        {
            log.ForSource("cli").Named("x").Information("line");
        }

        Assert.False(opened);
        Assert.Single(Lines());
    }

    [Fact]
    public void AStoreThatWillNotOpenCostsNoLine()
    {
        var opens = 0;
        using (
            var log = RtokLog.Create(
                Section(),
                new RtokLogOptions
                {
                    Debug = "off",
                    OpenStore = () =>
                    {
                        opens++;
                        throw new IOException("locked");
                    },
                }
            )
        )
        {
            log.ForSource("cli").Named("a").Information("one");
            log.ForSource("cli").Named("b").Information("two");
        }

        Assert.Equal(1, opens);
        Assert.Equal(2, Lines().Count);
    }

    [Fact]
    public void AFileThatCannotBeWrittenCostsNoRow()
    {
        File.WriteAllText(_dir.Combine("logs"), "a file where the log directory should be");
        using (var log = Create(store: true))
        {
            log.ForSource("cli").Named("x").Information("row only");
        }

        using var db = Read();
        Assert.Equal("row only", Assert.Single(db.Logs.ToList()).Message);
    }

    [Fact]
    public void APathThatCannotBeAFileLeavesALoggerWithoutAFile()
    {
        var section = Section();
        section.Path = "";
        using var log = RtokLog.Create(section, new RtokLogOptions { Debug = "off" });

        log.ForSource("cli").Named("x").Information("nowhere");
    }

    [Fact]
    public void RtokLogWritesBelowTheFloorToItsWriterOnly()
    {
        var stderr = new StringWriter();
        using (
            var log = RtokLog.Create(
                Section(),
                new RtokLogOptions { Debug = "debug", DebugWriter = stderr }
            )
        )
        {
            log.ForSource("cli").Named("argv").Debug("argv {Argv}", "logs");
        }

        Assert.EndsWith(
            " debug cli/argv: argv logs\n",
            stderr.ToString(),
            StringComparison.Ordinal
        );
        Assert.Empty(Lines());
    }

    [Theory]
    [InlineData("off")]
    [InlineData("0")]
    [InlineData("")]
    public void RtokLogOffWritesNothing(string value)
    {
        var stderr = new StringWriter();
        using (
            var log = RtokLog.Create(
                Section(),
                new RtokLogOptions { Debug = value, DebugWriter = stderr }
            )
        )
        {
            log.ForSource("cli").Named("x").Error("an error still goes to the file only");
        }

        Assert.Empty(stderr.ToString());
        Assert.Single(Lines());
    }

    [Fact]
    public void AnILoggerCategoryAndEventNameBecomeSourceAndName()
    {
        using (var log = Create())
        {
            using var factory = RtokLog.LoggerFactory(log);
            factory
                .CreateLogger("Rtok.Proxy")
                .LogInformation(new EventId(4, "request"), "GET {Path}", "/v1");
        }

        var line = Assert.Single(Lines());
        Assert.EndsWith(" info Rtok.Proxy/request: GET /v1", line, StringComparison.Ordinal);
    }

    [Fact]
    public void LibraryInformationStaysOutUnlessTheFloorIsDebug()
    {
        using (var log = Create())
        {
            using var factory = RtokLog.LoggerFactory(log);
            factory.CreateLogger("Microsoft.EntityFrameworkCore.Database").LogInformation("sql");
            factory.CreateLogger("Microsoft.EntityFrameworkCore.Database").LogWarning("slow");
        }
        var line = Assert.Single(Lines());
        Assert.EndsWith(": slow", line, StringComparison.Ordinal);

        File.Delete(LogPath);
        using (var log = Create(level: "debug"))
        {
            using var factory = RtokLog.LoggerFactory(log);
            factory.CreateLogger("Microsoft.EntityFrameworkCore.Database").LogInformation("sql");
        }
        Assert.Single(Lines());
    }

    [Fact]
    public void EfCoreLogsThroughTheStoreOptionsFactory()
    {
        using (var log = Create(level: "debug"))
        {
            using var factory = RtokLog.LoggerFactory(log);
            using var store = RtokStore.Open(new StoreOptions(DbPath) { LoggerFactory = factory });
            store.InsertLog("info", "cli", "x", "row");
        }

        Assert.Contains(
            Lines(),
            l => l.Contains(" Microsoft.EntityFrameworkCore.", StringComparison.Ordinal)
        );
    }

    public void Dispose() => _dir.Dispose();

    private Serilog.Core.Logger Create(string level = "info", bool store = false) =>
        RtokLog.Create(
            Section(level, toDb: store),
            new RtokLogOptions
            {
                Debug = "off",
                OpenStore = store ? () => RtokStore.Open(new StoreOptions(DbPath)) : null,
            }
        );

    private LogSection Section(string level = "info", bool toDb = true) =>
        new()
        {
            Path = LogPath,
            Level = level,
            ToDb = toDb,
        };

    private List<string> Lines() => File.Exists(LogPath) ? [.. File.ReadAllLines(LogPath)] : [];

    private RtokDbContext Read() =>
        new(
            new DbContextOptionsBuilder<RtokDbContext>()
                .UseSqlite($"Data Source={DbPath};Pooling=False")
                .Options
        );
}
