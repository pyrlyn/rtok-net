using Microsoft.Data.Sqlite;
using Rtok.Config;
using Rtok.Log;
using Rtok.Store;

namespace Rtok.AotTests;

// The log pipeline on NativeAOT (T10): Serilog's file line, rotation at max_bytes and the to_db
// row all run with no reflection the trimmer dropped.
public sealed class LogTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("rtok-aot-log-");

    public void Dispose() => _dir.Delete(recursive: true);

    [Fact]
    public void A_small_max_bytes_rotates_and_rtoks_own_lines_reach_the_store()
    {
        var path = Path.Combine(_dir.FullName, "logs", "rtok.log");
        var db = Path.Combine(_dir.FullName, "rtok.db");
        var section = new LogSection
        {
            Path = path,
            MaxBytes = 1024,
            ToDb = true,
        };
        using (
            var log = RtokLog.Create(
                section,
                new RtokLogOptions
                {
                    Debug = "off",
                    OpenStore = () => RtokStore.Open(new StoreOptions(db)),
                }
            )
        )
        {
            for (var i = 0; i < 40; i++)
            {
                log.ForSource("cli").Named("aot").Information("line {I:D2}", i);
            }
        }

        Assert.True(File.Exists(path + ".1"));
        Assert.EndsWith(
            " info cli/aot: line 39",
            File.ReadAllLines(path)[^1],
            StringComparison.Ordinal
        );
        using var connection = new SqliteConnection($"Data Source={db};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM logs WHERE source = 'cli' AND name = 'aot'";
        Assert.Equal(40L, command.ExecuteScalar());
    }
}
