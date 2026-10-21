using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Rtok.Store;

/// <summary>Every PRAGMA the store runs, in one place: EF has no API for them, and they are the
/// only SQL the store writes itself (README N7).</summary>
internal static class Pragmas
{
    /// <summary>SQLite's "database is locked" and "database table is locked".</summary>
    private const int Busy = 5;
    private const int Locked = 6;

    public static void BusyTimeout(DbConnection connection, TimeSpan wait) =>
        Execute(connection, $"PRAGMA busy_timeout = {(long)wait.TotalMilliseconds};");

    /// <summary>Only a new file can take incremental auto-vacuum without a full VACUUM.</summary>
    public static void IncrementalAutoVacuum(DbConnection connection) =>
        Execute(connection, "PRAGMA auto_vacuum = INCREMENTAL;");

    /// <summary>WAL, normal sync and foreign keys. Switching to WAL needs a moment with no other
    /// connection, so a locked file is retried <paramref name="attempts"/> times.</summary>
    public static void Steady(DbConnection connection, int attempts)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Execute(connection, "PRAGMA journal_mode = WAL;");
                break;
            }
            catch (SqliteException e)
                when (e.SqliteErrorCode is Busy or Locked && attempt < attempts)
            {
                Thread.Sleep(100);
            }
        }
        Execute(connection, "PRAGMA synchronous = NORMAL;");
        ForeignKeys(connection, on: true);
    }

    public static void ForeignKeys(DbConnection connection, bool on) =>
        Execute(connection, on ? "PRAGMA foreign_keys = ON;" : "PRAGMA foreign_keys = OFF;");

    /// <summary>True when any row breaks a foreign key: a migration that rebuilt a table with the
    /// keys off must not commit such a row.</summary>
    public static bool ForeignKeyViolations(DbConnection connection, DbTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA foreign_key_check;";
        using var reader = command.ExecuteReader();
        return reader.Read();
    }

    /// <summary>2 when the file runs incremental auto-vacuum.</summary>
    public static long AutoVacuum(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA auto_vacuum;";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>Returns the pages retention freed to the file system.</summary>
    public static void IncrementalVacuum(DbConnection connection) =>
        Execute(connection, "PRAGMA incremental_vacuum;");

    /// <summary>Turns an older file to incremental auto-vacuum: the mode takes effect only after a
    /// full VACUUM.</summary>
    public static void ConvertToIncrementalVacuum(DbConnection connection)
    {
        IncrementalAutoVacuum(connection);
        Execute(connection, "VACUUM;");
    }

    private static void Execute(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
