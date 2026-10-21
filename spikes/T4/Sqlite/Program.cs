// sqlite-probe <db>: open, insert one calls row, count by event.
// sqlite-probe <db> fts5: prove the linked SQLite has FTS5; prints the SQLite version.
using Microsoft.Data.Sqlite;

using var c = new SqliteConnection($"Data Source={args[0]};Pooling=False");
c.Open();
if (args is [_, "fts5"])
{
    using var fts = c.CreateCommand();
    fts.CommandText =
        "CREATE VIRTUAL TABLE IF NOT EXISTS n USING fts5(body); INSERT INTO n VALUES ('hook latency');"
        + "SELECT sqlite_version() || ' fts5 ' || count(*) FROM n WHERE n MATCH 'latency';";
    Console.Out.WriteLine(fts.ExecuteScalar());
    return 0;
}
using var cmd = c.CreateCommand();
cmd.CommandText =
    "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5;"
    + "CREATE TABLE IF NOT EXISTS Calls (Id INTEGER PRIMARY KEY AUTOINCREMENT, Ts INTEGER NOT NULL,"
    + " Surface TEXT NOT NULL, Event TEXT NOT NULL, Tool TEXT NOT NULL, InputBytes INTEGER NOT NULL);"
    + "INSERT INTO Calls (Ts, Surface, Event, Tool, InputBytes) VALUES ($ts, 'hook', 'PreToolUse', 'Bash', 1024);";
cmd.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
cmd.ExecuteNonQuery();
using var count = c.CreateCommand();
count.CommandText = "SELECT count(*) FROM Calls WHERE Event = 'PreToolUse';";
var n = (long)count.ExecuteScalar()!;
Console.Out.Write(n > 0 ? "{}" : "");
return 0;
