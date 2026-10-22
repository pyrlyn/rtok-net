using Microsoft.EntityFrameworkCore;
using Rtok.Config;
using Rtok.Store;
using Rtok.Store.Model;
using Rtok.Testing;

namespace Rtok.Archive.Tests;

/// <summary>A store with an archive directory in a fresh temp directory, the config the
/// expander reads, and a second context on the same file for assertions.</summary>
internal sealed class ArchiveFixture : IDisposable
{
    private readonly TempDir _dir = new("rtok-archive-");
    private RtokStore? _store;

    public ArchiveFixture()
    {
        Options = new StoreOptions(_dir.Combine("data", "rtok.db"))
        {
            ArchiveDir = _dir.Combine("data", "archive"),
        };
    }

    public StoreOptions Options { get; }

    public string ArchiveDir => Options.ArchiveDir!;

    public RtokConfig Config { get; } = new();

    public RtokStore Store => _store ??= RtokStore.Open(Options);

    /// <summary>Runs the shared handler as <c>rtok expand</c> does.</summary>
    public ExpandResult Expand(
        string id,
        string? lines = null,
        string? grep = null,
        int context = 0
    ) =>
        new Expander(Store, Config, Expander.CliSession).Run(
            new ExpandRequest(id, lines, grep, context)
        );

    /// <summary>A separate context on the database file; the store is opened first, so the
    /// schema exists.</summary>
    public RtokDbContext Read()
    {
        _ = Store;
        return new(
            new DbContextOptionsBuilder<RtokDbContext>()
                .UseSqlite($"Data Source={Options.DbPath};Pooling=False")
                .Options
        );
    }

    public void Dispose()
    {
        _store?.Dispose();
        _dir.Dispose();
    }
}
