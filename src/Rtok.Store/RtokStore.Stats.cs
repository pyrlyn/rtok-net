using System.Text;

namespace Rtok.Store;

public sealed partial class RtokStore
{
    /// <summary>The <c>usage</c> counters summed per api, ordered by api.</summary>
    public IReadOnlyList<UsageGroup> UsageByApi()
    {
        using var scope = Enter();
        var db = scope.Db;
        return Totals(db.Usage.ToList(), u => u.Api);
    }

    /// <summary>
    /// The <c>usage</c> counters summed per model, ordered by model; a row with no model counts
    /// as <c>unknown</c>.
    /// </summary>
    public IReadOnlyList<UsageGroup> UsageByModel()
    {
        using var scope = Enter();
        var db = scope.Db;
        return Totals(db.Usage.ToList(), u => u.Model ?? "unknown");
    }

    /// <summary>Every <c>measurements</c> row of <paramref name="plugin"/>, oldest first.</summary>
    public IReadOnlyList<MeasurementInput> MeasurementsOf(string plugin)
    {
        using var scope = Enter();
        var db = scope.Db;
        var name = plugin;
        return
        [
            .. db
                .Measurements.Where(m => m.Plugin == name)
                .OrderBy(m => m.Id)
                .AsEnumerable()
                .Select(Input),
        ];
    }

    /// <summary>
    /// The session ids named by <c>checkpoint:&lt;id&gt;</c> and <c>session:&lt;id&gt;</c> note
    /// titles, distinct and sorted.
    /// </summary>
    public IReadOnlyList<string> CheckpointSessionIds()
    {
        using var scope = Enter();
        var db = scope.Db;
        var titles = db
            .Notes.Where(n => n.Title.StartsWith("checkpoint:") || n.Title.StartsWith("session:"))
            .Select(n => n.Title)
            .ToList();
        return
        [
            .. titles
                .Select(t => t[(t.IndexOf(':', StringComparison.Ordinal) + 1)..])
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>Every session with <c>usage</c> rows at or after <paramref name="since"/> (Unix
    /// seconds), by its first row: oldest first.</summary>
    public IReadOnlyList<string> UsageSessions(long since = 0)
    {
        using var scope = Enter();
        var db = scope.Db;
        var from = since;
        return
        [
            .. db
                .Usage.Where(u => u.Ts >= from)
                .GroupBy(u => u.Session)
                .OrderBy(g => g.Min(u => u.Id))
                .Select(g => g.Key)
                .AsEnumerable(),
        ];
    }

    /// <summary>One session's <c>usage</c> rows at or after <paramref name="since"/>, in request
    /// order.</summary>
    public IReadOnlyList<UsageTurn> UsageTurns(string session, long since = 0)
    {
        using var scope = Enter();
        var db = scope.Db;
        var id = session;
        var from = since;
        return
        [
            .. db
                .Usage.Where(u => u.Session == id && u.Ts >= from)
                .OrderBy(u => u.Id)
                .Select(u => new UsageTurn(u.CallId, u.Api, u.Input, u.CacheCreate, u.CacheRead))
                .AsEnumerable(),
        ];
    }

    /// <summary>
    /// The request body <c>call_io</c> recorded for a call: inline, or its archive file. Null
    /// when none was recorded, only its size was kept, or the file is gone.
    /// </summary>
    public byte[]? CallRequest(long callId)
    {
        using var scope = Enter();
        var db = scope.Db;
        var id = callId;
        var io = db
            .CallIo.Where(c => c.CallId == id)
            .Select(c => new RequestColumns(c.RequestRaw, c.RequestJson, c.RequestArchive))
            .FirstOrDefault();
        if (io is null)
        {
            return null;
        }
        if (io.RequestRaw is not null)
        {
            return io.RequestRaw;
        }
        if (io.RequestJson is not null)
        {
            return Encoding.UTF8.GetBytes(io.RequestJson);
        }
        if (io.RequestArchive is null || _options.ArchiveDir is not { } dir)
        {
            return null;
        }
        try
        {
            return File.ReadAllBytes(Path.Combine(dir, io.RequestArchive));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The proxy's archive pointers placed at or after <paramref name="since"/> (Unix
    /// seconds), and how many of them were expanded.</summary>
    public (long Decisions, long Expanded) ArchiveDecisionCounts(long since = 0)
    {
        using var scope = Enter();
        var db = scope.Db;
        var from = since;
        return (
            db.ArchiveDecisions.LongCount(d => d.Ts >= from),
            db.ArchiveDecisions.LongCount(d => d.Ts >= from && d.ExpandedTs != null)
        );
    }

    private static List<UsageGroup> Totals(List<Model.Usage> rows, Func<Model.Usage, string> key) =>
        [
            .. rows.GroupBy(key, StringComparer.Ordinal)
                .Select(g => new UsageGroup(
                    g.Key,
                    g.Sum(u => u.Input),
                    g.Sum(u => u.CacheCreate),
                    g.Sum(u => u.CacheRead),
                    g.Sum(u => u.Output)
                ))
                .OrderBy(t => t.Key, StringComparer.Ordinal),
        ];
}
