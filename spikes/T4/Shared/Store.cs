using Microsoft.EntityFrameworkCore;

namespace Probe;

// One calls row per hook dispatch, roughly the shape rtok writes (catalog A2).
public sealed class CallRow
{
    public long Id { get; set; }
    public long Ts { get; set; }
    public string Surface { get; set; } = "";
    public string Event { get; set; } = "";
    public string Tool { get; set; } = "";
    public long InputBytes { get; set; }
}

public sealed class StoreContext(string path) : DbContext
{
    public StoreContext()
        : this("design.db") { }

    public DbSet<CallRow> Calls => Set<CallRow>();

    protected override void OnConfiguring(DbContextOptionsBuilder o) =>
        o.UseSqlite($"Data Source={path};Pooling=False");

    public static StoreContext Open(string path)
    {
        var ctx = new StoreContext(path);
        ctx.Database.ExecuteSqlRaw(
            "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5;"
                + "CREATE TABLE IF NOT EXISTS Calls (Id INTEGER PRIMARY KEY AUTOINCREMENT, Ts INTEGER NOT NULL,"
                + " Surface TEXT NOT NULL, Event TEXT NOT NULL, Tool TEXT NOT NULL, InputBytes INTEGER NOT NULL);"
        );
        return ctx;
    }
}

// Design time (dotnet ef, and the Tasks package at publish) builds the context here instead of
// running the entry point.
public sealed class StoreContextFactory
    : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<StoreContext>
{
    public StoreContext CreateDbContext(string[] args) => new("design.db");
}

public static class StoreOps
{
    // The EF query precompiler (10.0.13) only takes locals: a context or a captured value that is
    // a method parameter fails with "Dynamic LINQ queries are not supported" or
    // "IdentifierName of type ParameterSymbol".
    public static int Record(
        this StoreContext ctx,
        string surface,
        string ev,
        string tool,
        long bytes
    )
    {
        var db = ctx;
        var e = ev;
        db.Calls.Add(
            new CallRow
            {
                Ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Surface = surface,
                Event = e,
                Tool = tool,
                InputBytes = bytes,
            }
        );
        db.SaveChanges();
        return db.Calls.Count(c => c.Event == e);
    }
}
