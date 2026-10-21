using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Rtok.Store.Model;

// Usage: Rtok.Store.Scripts <out-dir>. Writes <migration-id>.sql per migration (no transaction:
// the store's runner wraps each one) and _history.sql, the history table's create script.
if (args is not [var outDir])
{
    return 2;
}
Directory.CreateDirectory(outDir);
foreach (var stale in Directory.GetFiles(outDir, "*.sql"))
{
    File.Delete(stale);
}
using var db = new DesignTimeFactory().CreateDbContext([]);
var migrator = db.GetService<IMigrator>();
var previous = Migration.InitialDatabase;
foreach (var id in db.Database.GetMigrations())
{
    var script = migrator.GenerateScript(
        previous,
        id,
        MigrationsSqlGenerationOptions.NoTransactions
    );
    File.WriteAllText(Path.Combine(outDir, id + ".sql"), script);
    previous = id;
}
File.WriteAllText(
    Path.Combine(outDir, "_history.sql"),
    db.GetService<IHistoryRepository>().GetCreateIfNotExistsScript()
);
return 0;
