using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Rtok.Store.Model;

namespace Rtok.Store;

/// <summary>The one SQLite file that holds every ledger (rtok D8, D13, D18). Plugins never touch
/// it directly. One connection and one context; calls are serialized.</summary>
public sealed partial class RtokStore : IDisposable
{
    private readonly Lock _gate = new();
    private readonly SqliteConnection _connection;
    private readonly RtokDbContext _db;
    private readonly StoreOptions _options;

    private RtokStore(StoreOptions options, SqliteConnection connection, RtokDbContext db)
    {
        _options = options;
        _connection = connection;
        _db = db;
    }

    /// <summary>The database file, as a full path.</summary>
    public string DbPath => _options.DbPath;

    /// <summary>Opens or creates the store: an owner-only directory and file, the refusal of a
    /// file rtok-net did not make (README N15) before anything is written to it, then WAL,
    /// foreign keys and the pending migrations.</summary>
    /// <exception cref="StoreRefusedException">The file is another program's store or a newer one.</exception>
    public static RtokStore Open(StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var path = Path.GetFullPath(options.DbPath);
        options = options with { DbPath = path };
        StoreFiles.CreatePrivateDirectory(Path.GetDirectoryName(path) ?? "");
        var fresh = !File.Exists(path) || new FileInfo(path).Length == 0;
        if (fresh)
        {
            StoreFiles.CreatePrivateFile(path);
        }
        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
                DefaultTimeout = Math.Max(1, (int)Math.Ceiling(options.Busy.TotalSeconds)),
            }.ToString()
        );
        RtokDbContext? db = null;
        try
        {
            connection.Open();
            Pragmas.BusyTimeout(connection, options.Busy);
            var builder = new DbContextOptionsBuilder<RtokDbContext>().UseSqlite(connection);
            if (options.LoggerFactory is { } loggers)
            {
                builder.UseLoggerFactory(loggers);
            }
            db = new RtokDbContext(builder.Options);
            if (fresh)
            {
                Pragmas.IncrementalAutoVacuum(connection);
            }
            else
            {
                MigrationRunner.Refuse(db, path);
            }
            Pragmas.Steady(connection, options.ConnectAttempts);
            MigrationRunner.Migrate(connection, db, options, path);
            StoreFiles.RestrictDatabase(path);
            return new RtokStore(options, connection, db);
        }
        catch
        {
            db?.Dispose();
            connection.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _db.Dispose();
            _connection.Dispose();
        }
    }

    private long Now => _options.Time.GetUtcNow().ToUnixTimeSeconds();

    /// <summary>The store's lock and context for one call; the tracker is cleared on the way
    /// out. EF's query precompiler (10.0.13) takes only locals (research.md F), so every method
    /// copies <see cref="Scope.Db"/> and each value its queries capture into a local first.</summary>
    private Scope Enter() => new(this, write: false, maintenance: false);

    /// <summary><see cref="Enter"/> inside an immediate transaction: Microsoft.Data.Sqlite begins
    /// with <c>BEGIN IMMEDIATE</c>, so a read-then-write takes the write lock up front and cannot
    /// race another writer's upgrade (rtok T208). Nothing commits without <see cref="Scope.Commit"/>.</summary>
    private Scope EnterWrite() => new(this, write: true, maintenance: false);

    /// <summary><see cref="EnterWrite"/> under the maintenance wait: retention and vacuum take
    /// the writer lock with <see cref="StoreOptions.MaintenanceBusy"/> instead of the hook's
    /// short bound, which comes back on the way out whatever happened (rtok T75).</summary>
    private Scope EnterMaintenance() => new(this, write: true, maintenance: true);

    private readonly ref struct Scope
    {
        private readonly RtokStore _store;
        private readonly Lock.Scope _lock;
        private readonly IDbContextTransaction? _transaction;
        private readonly bool _maintenance;

        public Scope(RtokStore store, bool write, bool maintenance)
        {
            _store = store;
            _lock = store._gate.EnterScope();
            try
            {
                if (maintenance)
                {
                    Pragmas.BusyTimeout(store._connection, store._options.MaintenanceBusy);
                    _maintenance = true;
                }
                _transaction = write ? store._db.Database.BeginTransaction() : null;
            }
            catch
            {
                Restore();
                throw;
            }
        }

        public RtokDbContext Db => _store._db;

        public void Commit() => _transaction?.Commit();

        public void Dispose()
        {
            try
            {
                _transaction?.Dispose();
            }
            finally
            {
                _store._db.ChangeTracker.Clear();
                Restore();
            }
        }

        private void Restore()
        {
            try
            {
                if (_maintenance)
                {
                    Pragmas.BusyTimeout(_store._connection, _store._options.Busy);
                }
            }
            finally
            {
                _lock.Dispose();
            }
        }
    }
}
