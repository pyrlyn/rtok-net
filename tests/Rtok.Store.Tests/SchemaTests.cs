using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rtok.Store.Model;

namespace Rtok.Store.Tests;

public sealed class SchemaTests
{
    /// <summary>Every table, index, trigger and FTS5 table the migrations create, as SQLite
    /// stores them: a change to the model or a migration shows up here first.</summary>
    [Fact]
    public async Task The_schema_matches_the_snapshot()
    {
        using var h = new StoreHarness();
        _ = h.Store;
        using var connection = new SqliteConnection(
            $"Data Source={h.Options.DbPath};Pooling=False"
        );
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT type, name, tbl_name, sql FROM sqlite_master ORDER BY type, name";
        using var reader = command.ExecuteReader();
        var schema = new StringBuilder();
        while (reader.Read())
        {
            schema.Append(reader.GetString(0)).Append(' ').Append(reader.GetString(1));
            schema.Append(" on ").Append(reader.GetString(2)).Append('\n');
            if (!reader.IsDBNull(3))
            {
                schema.Append(reader.GetString(3)).Append("\n\n");
            }
        }
        await Verify(schema.ToString());
    }

    [Fact]
    public void The_migrations_cover_the_model()
    {
        using var db = new DesignTimeFactory().CreateDbContext([]);

        Assert.False(
            db.Database.HasPendingModelChanges(),
            "run dotnet ef migrations add for the model change"
        );
    }

    [Fact]
    public void Every_migration_has_an_embedded_script()
    {
        using var db = new DesignTimeFactory().CreateDbContext([]);

        Assert.Equal(db.Database.GetMigrations(), MigrationRunner.Known, StringComparer.Ordinal);
    }
}
