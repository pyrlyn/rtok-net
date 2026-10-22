using Rtok.Store.Model;

namespace Rtok.Store;

public sealed partial class RtokStore
{
    /// <summary>The model summary of <paramref name="archiveId"/> written under compressor
    /// <paramref name="version"/>, or <see langword="null"/> when none is stored.</summary>
    public string? GetArchiveSummary(string archiveId, string version)
    {
        using var scope = Enter();
        var db = scope.Db;
        var (id, v) = (archiveId, version);
        return db
            .ArchiveSummaries.Where(s => s.ArchiveId == id && s.Version == v)
            .Select(s => s.Summary)
            .FirstOrDefault();
    }

    /// <summary>Stores a model summary of an archived body. First writer wins: a summary never
    /// changes once stored, so the block it replaces stays byte-stable for the prompt cache.
    /// Returns whether this call stored it.</summary>
    public bool PutArchiveSummary(string archiveId, string version, string summary)
    {
        using var scope = EnterWrite();
        var db = scope.Db;
        var (id, v) = (archiveId, version);
        if (db.ArchiveSummaries.Any(s => s.ArchiveId == id && s.Version == v))
        {
            return false;
        }
        db.ArchiveSummaries.Add(
            new ArchiveSummary
            {
                ArchiveId = archiveId,
                Version = version,
                Summary = summary,
                CreatedAt = Now,
            }
        );
        db.SaveChanges();
        scope.Commit();
        return true;
    }
}
