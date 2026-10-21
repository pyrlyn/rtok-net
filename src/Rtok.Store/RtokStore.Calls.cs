using Microsoft.EntityFrameworkCore;
using Rtok.Store.Model;

namespace Rtok.Store;

public sealed partial class RtokStore
{
    /// <summary>Starts a <c>calls</c> row and returns its id.</summary>
    public long InsertCall(
        string sessionId,
        string surface,
        string kind,
        long? hostId = null,
        long? providerId = null,
        long? modelId = null,
        string? plugin = null,
        string? name = null
    )
    {
        using var scope = Enter();
        var db = scope.Db;
        var call = new CallRow
        {
            Ts = Now,
            SessionId = sessionId,
            Surface = surface,
            Kind = kind,
            HostId = hostId,
            ProviderId = providerId,
            ModelId = modelId,
            Plugin = plugin,
            Name = name,
            Ok = true,
        };
        db.Calls.Add(call);
        db.SaveChanges();
        return call.Id;
    }

    /// <summary>Nests a <c>plugin_run</c> call under the hook, MCP call or API request it ran in.</summary>
    public void SetCallParent(long id, long parentId)
    {
        using var scope = Enter();
        var db = scope.Db;
        var callId = id;
        long? parent = parentId;
        db.Calls.Where(c => c.Id == callId)
            .ExecuteUpdate(s => s.SetProperty(c => c.ParentId, _ => parent));
    }

    public void SetCallMs(long id, double ms)
    {
        using var scope = Enter();
        var db = scope.Db;
        var callId = id;
        double? value = ms;
        db.Calls.Where(c => c.Id == callId)
            .ExecuteUpdate(s => s.SetProperty(c => c.Ms, _ => value));
    }

    /// <summary>Re-tags a call once its response showed what it was.</summary>
    public void SetCallKind(long id, string kind)
    {
        using var scope = Enter();
        var db = scope.Db;
        var (callId, value) = (id, kind);
        db.Calls.Where(c => c.Id == callId).ExecuteUpdate(s => s.SetProperty(c => c.Kind, value));
    }

    /// <summary>The tier the provider reported serving the request on.</summary>
    public void SetCallServiceTier(long id, string tier)
    {
        using var scope = Enter();
        var db = scope.Db;
        var (callId, value) = (id, tier);
        db.Calls.Where(c => c.Id == callId)
            .ExecuteUpdate(s => s.SetProperty(c => c.ServiceTier, value));
    }

    /// <summary>A failed call: <c>ok = 0</c> and the error text.</summary>
    public void SetCallError(long id, string error)
    {
        using var scope = EnterWrite();
        var db = scope.Db;
        var (callId, value, ok) = (id, error, false);
        // One setter per statement: EF's precompiler (10.0.13) reads several setters' captured
        // values in reverse order (research.md F).
        db.Calls.Where(c => c.Id == callId).ExecuteUpdate(s => s.SetProperty(c => c.Ok, _ => ok));
        db.Calls.Where(c => c.Id == callId).ExecuteUpdate(s => s.SetProperty(c => c.Error, value));
        scope.Commit();
    }

    public void InsertTokens(
        long callId,
        string? plugin,
        string phase,
        string source,
        long tokens
    ) =>
        Add(
            new TokenRow
            {
                Ts = Now,
                CallId = callId,
                Plugin = plugin,
                Phase = phase,
                SourceName = source,
                Tokens = tokens,
            }
        );

    /// <summary>The provider's counters for an API request: one <c>tokens</c> row with phase
    /// <c>after</c> and source <c>provider</c>. The counters are disjoint on every wire.</summary>
    public void InsertProviderTokens(
        long callId,
        long total,
        long input,
        long cacheCreate,
        long cacheRead,
        long output
    ) =>
        Add(
            new TokenRow
            {
                Ts = Now,
                CallId = callId,
                Phase = "after",
                SourceName = "provider",
                Tokens = total,
                Input = input,
                Output = output,
                CacheCreate = cacheCreate,
                CacheRead = cacheRead,
            }
        );

    /// <summary>The proxy's ground truth: one <c>usage</c> row per API request.</summary>
    public void InsertUsage(
        string session,
        string? model,
        string api,
        long input,
        long cacheCreate,
        long cacheRead,
        long output,
        long callId
    ) =>
        Add(
            NewUsage(
                session,
                callId,
                new UsageInput(model, api, input, cacheCreate, cacheRead, output, null)
            )
        );

    /// <summary>Many <c>usage</c> rows for one call in one transaction (a Batch results file
    /// carries one per request). A row whose key is already stored, or repeated in
    /// <paramref name="rows"/>, is skipped, so a results file fetched again counts each result
    /// once; a row without a key always lands. Returns how many rows were written.</summary>
    public int InsertUsageRows(string session, long callId, IReadOnlyList<UsageInput> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        using var scope = EnterWrite();
        var db = scope.Db;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var written = 0;
        foreach (var row in rows)
        {
            if (row.ResultKey is { } key)
            {
                var resultKey = key;
                if (!seen.Add(key) || db.Usage.Any(u => u.ResultKey == resultKey))
                {
                    continue;
                }
            }
            db.Usage.Add(NewUsage(session, callId, row));
            written++;
        }
        db.SaveChanges();
        scope.Commit();
        return written;
    }

    public void InsertLog(
        string level,
        string source,
        string name,
        string message,
        string? session = null,
        long? callId = null,
        string? plugin = null
    ) =>
        Add(
            new LogRow
            {
                Ts = Now,
                Level = level,
                SourceName = source,
                Name = name,
                Message = message,
                Session = session,
                CallId = callId,
                Plugin = plugin,
            }
        );

    /// <summary>One new row, saved on its own.</summary>
    private void Add<T>(T row)
        where T : class
    {
        using var scope = Enter();
        var db = scope.Db;
        db.Add(row);
        db.SaveChanges();
    }

    private Usage NewUsage(string session, long callId, UsageInput row) =>
        new()
        {
            Ts = Now,
            Session = session,
            Model = row.Model,
            Api = row.Api,
            Input = row.Input,
            CacheCreate = row.CacheCreate,
            CacheRead = row.CacheRead,
            Output = row.Output,
            CallId = callId,
            ResultKey = row.ResultKey,
        };
}
