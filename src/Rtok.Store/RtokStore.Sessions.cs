using Rtok.Store.Model;

namespace Rtok.Store;

public sealed partial class RtokStore
{
    /// <summary>Inserts the session or fills its unknown attribution. A non-null value already
    /// stored stays: a later writer that does not know it (the proxy and MCP pass no project or
    /// cwd) must not wipe what an earlier hook set.</summary>
    public void UpsertSession(string id, long? hostId, string? project, string? cwd, string? source)
    {
        using var scope = EnterWrite();
        var db = scope.Db;
        var sessionId = id;
        var row = db.Sessions.FirstOrDefault(s => s.Id == sessionId);
        if (row is null)
        {
            db.Sessions.Add(
                new Session
                {
                    Id = id,
                    HostId = hostId,
                    Project = project,
                    Cwd = cwd,
                    SourceName = source,
                    StartedAt = Now,
                }
            );
        }
        else
        {
            row.HostId ??= hostId;
            row.Project ??= project;
            row.Cwd ??= cwd;
            row.SourceName ??= source;
        }
        db.SaveChanges();
        scope.Commit();
    }

    /// <summary>Marks the session ended now (<c>sessions.ended_at</c>), inserting it when no
    /// hook saw it start. A repeat end moves the time on.</summary>
    public void EndSession(string id, long? hostId)
    {
        using var scope = EnterWrite();
        var db = scope.Db;
        var sessionId = id;
        var row = db.Sessions.FirstOrDefault(s => s.Id == sessionId);
        if (row is null)
        {
            db.Sessions.Add(
                new Session
                {
                    Id = id,
                    HostId = hostId,
                    StartedAt = Now,
                    EndedAt = Now,
                }
            );
        }
        else
        {
            row.HostId ??= hostId;
            row.EndedAt = Now;
        }
        db.SaveChanges();
        scope.Commit();
    }

    /// <summary>The provider and model ids for two slugs, inserted on first sight; a new
    /// provider is named by its slug.</summary>
    public (long ProviderId, long ModelId) UpsertModel(string providerSlug, string modelSlug)
    {
        using var scope = EnterWrite();
        var db = scope.Db;
        var (provider, model) = (providerSlug, modelSlug);
        var providerId = db
            .Providers.Where(p => p.Slug == provider)
            .Select(p => p.Id)
            .FirstOrDefault();
        if (providerId == 0)
        {
            var row = new Provider
            {
                Slug = provider,
                Name = provider,
                CreatedAt = Now,
            };
            db.Providers.Add(row);
            db.SaveChanges();
            providerId = row.Id;
        }
        var owner = providerId;
        var modelId = db
            .Models.Where(m => m.ProviderId == owner && m.Slug == model)
            .Select(m => m.Id)
            .FirstOrDefault();
        if (modelId == 0)
        {
            var row = new LlmModel
            {
                ProviderId = providerId,
                Slug = model,
                CreatedAt = Now,
            };
            db.Models.Add(row);
            db.SaveChanges();
            modelId = row.Id;
        }
        scope.Commit();
        return (providerId, modelId);
    }

    /// <summary>The id of a seeded host slug (<c>claude</c>, <c>cursor</c>, …), or null.</summary>
    public long? HostId(string slug)
    {
        using var scope = Enter();
        var db = scope.Db;
        var hostSlug = slug;
        return db.Hosts.Where(h => h.Slug == hostSlug).Select(h => (long?)h.Id).FirstOrDefault();
    }
}
