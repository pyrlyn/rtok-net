using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Rtok.Store.Model;

namespace Rtok.Store;

/// <summary>Applies the embedded, EF-generated migration scripts (README N7): <c>Migrate()</c>
/// does not run under NativeAOT. <c>__EFMigrationsHistory</c> is the source of truth.</summary>
internal static class MigrationRunner
{
    private const string Prefix = "Rtok.Store.Migrations.";
    private const string HistoryScript = Prefix + "_history.sql";
    private const string HistoryTable = "__EFMigrationsHistory";

    /// <summary>Every migration this build knows, oldest first (EF ids sort by time).</summary>
    public static IReadOnlyList<string> Known { get; } =
    [
        .. typeof(MigrationRunner)
            .Assembly.GetManifestResourceNames()
            .Where(n =>
                n.StartsWith(Prefix, StringComparison.Ordinal)
                && !string.Equals(n, HistoryScript, StringComparison.Ordinal)
            )
            .Select(n => n[Prefix.Length..^".sql".Length])
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>Refuses a file rtok-net did not create or a newer rtok-net migrated (README N15).
    /// Reads only: the file is left as it was.</summary>
    public static void Refuse(RtokDbContext context, string path)
    {
        var db = context;
        if (!db.SchemaObjects.Any(o => o.Type == "table"))
        {
            return;
        }
        if (!HasHistory(db))
        {
            throw new StoreRefusedException(
                path,
                "it has tables but no migration history, so rtok-net did not create it (the Rust rtok's store?)"
            );
        }
        RefuseUnknown(Applied(db), path);
    }

    /// <summary>Applies the pending migrations in one write transaction, with foreign keys off as
    /// EF's table rebuilds expect and checked before the commit.</summary>
    public static void Migrate(
        DbConnection connection,
        RtokDbContext db,
        StoreOptions options,
        string path
    )
    {
        if (HasHistory(db) && Applied(db).IsSupersetOf(Known))
        {
            return;
        }
        Pragmas.BusyTimeout(connection, options.MaintenanceBusy);
        Pragmas.ForeignKeys(connection, on: false);
        try
        {
            using var transaction = db.Database.BeginTransaction();
            var dbTransaction = transaction.GetDbTransaction();
            Execute(connection, dbTransaction, Script(HistoryScript), options);
            // Read again under the write lock: another process may have migrated meanwhile.
            var applied = Applied(db);
            RefuseUnknown(applied, path);
            foreach (var id in Known.Where(id => !applied.Contains(id)))
            {
                Execute(connection, dbTransaction, Script(Prefix + id + ".sql"), options);
            }
            if (Pragmas.ForeignKeyViolations(connection, dbTransaction))
            {
                throw new InvalidOperationException(
                    $"{path}: a migration left rows that break a foreign key"
                );
            }
            transaction.Commit();
        }
        finally
        {
            Pragmas.ForeignKeys(connection, on: true);
            Pragmas.BusyTimeout(connection, options.Busy);
        }
    }

    private static bool HasHistory(RtokDbContext context)
    {
        var db = context;
        var name = HistoryTable;
        return db.SchemaObjects.Any(o => o.Type == "table" && o.Name == name);
    }

    private static HashSet<string> Applied(RtokDbContext context)
    {
        var db = context;
        // ToList ends the query where the precompiler sees it; a bare spread enumerates it unseen.
        return [.. db.MigrationHistory.Select(h => h.MigrationId).ToList()];
    }

    private static void RefuseUnknown(HashSet<string> applied, string path)
    {
        var unknown = applied
            .Except(Known, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
        if (unknown is not null)
        {
            throw new StoreRefusedException(
                path,
                $"a newer rtok-net migrated it ({unknown} is not a migration this build knows)"
            );
        }
    }

    private static string Script(string name)
    {
        using var stream =
            typeof(MigrationRunner).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"missing embedded migration script {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void Execute(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        StoreOptions options
    )
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = (int)Math.Ceiling(options.MaintenanceBusy.TotalSeconds);
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
