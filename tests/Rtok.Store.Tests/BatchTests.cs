using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace Rtok.Store.Tests;

public sealed class BatchTests
{
    private static StoreHarness FailFast() =>
        new(o =>
            o with
            {
                FailFast = true,
                Busy = TimeSpan.FromMilliseconds(5),
                MaintenanceBusy = TimeSpan.FromMilliseconds(5),
                ConnectAttempts = 1,
            }
        );

    [Fact]
    public void A_batch_commits_every_write_once()
    {
        using var h = FailFast();
        var before = h.Store.Commits;

        using (var batch = h.Store.BeginBatch())
        {
            h.Store.UpsertSession("s", null, null, null, null);
            var call = h.Store.InsertCall("s", "hook", "hook", name: "PreToolUse");
            h.Store.InsertMeasurements("s", [Row("p")], once: "PreToolUse:t1");
            h.Store.InsertHookCallIo(
                call,
                """{"session_id":"s","tool_name":"Bash"}"""u8.ToArray(),
                "{}"u8.ToArray()
            );
            using (var db = h.Read())
            {
                Assert.Empty(db.Calls);
            }
            batch.Commit();
        }

        Assert.Equal(before + 1, h.Store.Commits);
        using var read = h.Read();
        Assert.Single(read.Calls);
        Assert.Single(read.Measurements);
        Assert.Single(read.CallIo);
    }

    [Fact]
    public void A_batch_disposed_without_commit_writes_nothing()
    {
        using var h = FailFast();
        h.Store.UpsertSession("s", null, null, null, null);

        using (h.Store.BeginBatch())
        {
            h.Store.InsertCall("s", "hook", "hook");
        }

        using var db = h.Read();
        Assert.Empty(db.Calls);
    }

    [Fact]
    public void A_failed_write_inside_a_batch_leaves_the_others()
    {
        using var h = FailFast();
        h.Store.UpsertSession("s", null, null, null, null);

        using (var batch = h.Store.BeginBatch())
        {
            var call = h.Store.InsertCall("s", "hook", "hook");
            // No such call: the foreign key fails this write only.
            Assert.ThrowsAny<Exception>(() =>
                h.Store.InsertHookCallIo(call + 1000, "{}"u8.ToArray(), null)
            );
            h.Store.InsertMeasurements("s", [Row("p")]);
            batch.Commit();
        }

        using var db = h.Read();
        Assert.Single(db.Calls);
        Assert.Single(db.Measurements);
        Assert.Empty(db.CallIo);
    }

    [Fact]
    public void A_locked_store_refuses_a_fail_fast_batch_within_milliseconds()
    {
        using var h = FailFast();
        h.Store.UpsertSession("s", null, null, null, null);
        using var holder = HoldWriter(h.Options.DbPath);

        var clock = Stopwatch.StartNew();
        var e = Assert.Throws<StoreLockedException>(() => h.Store.BeginBatch());
        clock.Stop();

        Assert.True(RtokStore.IsLocked(e));
        // The driver's own retry would take a second at least.
        Assert.InRange(clock.ElapsedMilliseconds, 0, 500);
        // The store is usable again once the lock goes.
        holder.Dispose();
        using var batch = h.Store.BeginBatch();
        h.Store.InsertCall("s", "hook", "hook");
        batch.Commit();
    }

    [Fact]
    public void A_fail_fast_store_opens_while_another_connection_writes()
    {
        using var h = FailFast();
        h.Store.UpsertSession("s", null, null, null, null);
        h.Reopen();
        using var holder = HoldWriter(h.Options.DbPath);

        var clock = Stopwatch.StartNew();
        _ = h.Store;

        Assert.InRange(clock.ElapsedMilliseconds, 0, 500);
    }

    [Fact]
    public void Only_one_batch_is_open_at_a_time()
    {
        using var h = FailFast();
        using var batch = h.Store.BeginBatch();

        Assert.Throws<InvalidOperationException>(() => h.Store.BeginBatch());
    }

    [Fact]
    public void Is_locked_reads_the_inner_exception()
    {
        Assert.True(
            RtokStore.IsLocked(new InvalidOperationException("x", new SqliteException("busy", 5)))
        );
        Assert.False(RtokStore.IsLocked(new SqliteException("other", 1)));
        Assert.False(RtokStore.IsLocked(null));
    }

    /// <summary>Another connection holding the writer lock until disposed.</summary>
    internal static IDisposable HoldWriter(string dbPath)
    {
        var connection = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        connection.Open();
        var transaction = connection.BeginTransaction();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO kv (key, value) VALUES ('lock-holder', '1');";
            command.ExecuteNonQuery();
        }
        return new Holder(connection, transaction);
    }

    private static MeasurementInput Row(string plugin) => new(plugin, "k", 10, 5, 3, 1, null, null);

    private sealed class Holder(SqliteConnection connection, SqliteTransaction transaction)
        : IDisposable
    {
        public void Dispose()
        {
            transaction.Dispose();
            connection.Dispose();
        }
    }
}
