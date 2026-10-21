using Microsoft.EntityFrameworkCore;
using Rtok.Store.Model;
using Rtok.Testing;

namespace Rtok.Store.Tests;

/// <summary>A store in a fresh temp directory on a clock the test moves, plus a second context
/// on the same file to read back what the store wrote.</summary>
internal sealed class StoreHarness : IDisposable
{
    private readonly TempDir _dir = new("rtok-store-");
    private RtokStore? _store;

    public StoreHarness(Func<StoreOptions, StoreOptions>? configure = null)
    {
        Options = new StoreOptions(_dir.Combine("data", "rtok.db"))
        {
            ArchiveDir = _dir.Combine("data", "archive"),
            Time = Clock,
        };
        if (configure is not null)
        {
            Options = configure(Options);
        }
    }

    public ManualClock Clock { get; } = new(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));

    public StoreOptions Options { get; }

    public string Dir => _dir.Path;

    /// <summary>A path inside the test's directory; nothing is created.</summary>
    public string Combine(params ReadOnlySpan<string> parts) => _dir.Combine(parts);

    public RtokStore Store => _store ??= RtokStore.Open(Options);

    /// <summary>A separate context on the database file, for assertions.</summary>
    public RtokDbContext Read() =>
        new(
            new DbContextOptionsBuilder<RtokDbContext>()
                .UseSqlite($"Data Source={Options.DbPath};Pooling=False")
                .Options
        );

    public void Reopen()
    {
        _store?.Dispose();
        _store = null;
    }

    public void Dispose()
    {
        _store?.Dispose();
        _dir.Dispose();
    }
}
