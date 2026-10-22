using Microsoft.EntityFrameworkCore;
using Rtok.Store.Model;

namespace Rtok.Store;

public sealed partial class RtokStore
{
    /// <summary>Whether <paramref name="name"/> <paramref name="version"/> has been ingested.</summary>
    public bool DocCrateCached(string name, string version)
    {
        using var scope = Enter();
        var db = scope.Db;
        var (n, v) = (name, version);
        return db.DocCrates.Any(c => c.Name == n && c.Version == v);
    }

    /// <summary>
    /// The items of one package matching <paramref name="query"/> by FTS5, best bm25 rank first
    /// (ties by id), at most <paramref name="limit"/>, with their whole bodies. The query is
    /// searched as quoted words (<see cref="PhraseQuery"/>), so no text is an FTS5 syntax error;
    /// one with no word finds nothing.
    /// </summary>
    public IReadOnlyList<DocHit> SearchDocItems(
        string name,
        string version,
        string query,
        int limit
    )
    {
        if (PhraseQuery(query) is not { } match || limit <= 0)
        {
            return [];
        }
        using var scope = Enter();
        var db = scope.Db;
        var (n, v, take) = (name, version, limit);
        return db
            .DocItemMatches.Where(f =>
                f.Query == match && f.Item.CrateName == n && f.Item.Version == v
            )
            .OrderBy(f => f.Rank)
            .ThenBy(f => f.RowId)
            .Take(take)
            .Select(f => new DocHit(f.Item.Id, f.Item.Path, f.Item.Kind, f.Item.Docs))
            .ToList();
    }

    /// <summary>One ingested item by id, or null.</summary>
    public DocHit? GetDocItem(long id)
    {
        using var scope = Enter();
        var db = scope.Db;
        var key = id;
        return db
            .DocItems.Where(i => i.Id == key)
            .Select(i => new DocHit(i.Id, i.Path, i.Kind, i.Docs))
            .ToList()
            .FirstOrDefault();
    }

    /// <summary>
    /// Replaces a package and its items, unless the stored package already has
    /// <paramref name="crate"/>'s SHA-256: then nothing is touched, so the ids handed out stay
    /// valid. The compare and the write share one immediate transaction, so two ingests of the
    /// same bytes cannot both rewrite. Returns the items written, or null when unchanged.
    /// </summary>
    public int? SyncDocCrate(DocCrate crate, IReadOnlyList<DocEntry> items)
    {
        ArgumentNullException.ThrowIfNull(crate);
        ArgumentNullException.ThrowIfNull(items);
        using var scope = EnterWrite();
        var db = scope.Db;
        var (n, v) = (crate.Name, crate.Version);
        var stored = db
            .DocCrates.Where(c => c.Name == n && c.Version == v)
            .Select(c => c.Sha256)
            .ToList();
        if (stored.Count > 0 && string.Equals(stored[0], crate.Sha256, StringComparison.Ordinal))
        {
            return null;
        }
        Replace(scope, crate, items);
        scope.Commit();
        return items.Count;
    }

    /// <summary>Replaces a package and its items whatever is stored; returns the items written.</summary>
    public int ReplaceDocCrate(DocCrate crate, IReadOnlyList<DocEntry> items)
    {
        ArgumentNullException.ThrowIfNull(crate);
        ArgumentNullException.ThrowIfNull(items);
        using var scope = EnterWrite();
        Replace(scope, crate, items);
        scope.Commit();
        return items.Count;
    }

    /// <summary>Drops a package and its items (a local source that disappeared).</summary>
    public void DeleteDocCrate(string name, string version)
    {
        using var scope = EnterWrite();
        Delete(scope, name, version);
        scope.Commit();
    }

    /// <summary>
    /// <paramref name="text"/>'s words, each in FTS5 double quotes (a quote inside doubled), so
    /// operators and column filters in it are searched as text; null when it has no word.
    /// </summary>
    public static string? PhraseQuery(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0
            ? null
            : string.Join(
                ' ',
                words.Select(w => "\"" + w.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"")
            );
    }

    private static void Replace(Scope scope, DocCrate crate, IReadOnlyList<DocEntry> items)
    {
        Delete(scope, crate.Name, crate.Version);
        var db = scope.Db;
        db.DocCrates.Add(
            new DocCrate
            {
                Name = crate.Name,
                Version = crate.Version,
                Sha256 = crate.Sha256,
                FormatVersion = crate.FormatVersion,
                FetchedUnix = crate.FetchedUnix,
            }
        );
        foreach (var item in items)
        {
            db.DocItems.Add(
                new DocItem
                {
                    CrateName = crate.Name,
                    Version = crate.Version,
                    Path = item.Path,
                    Kind = item.Kind,
                    Docs = item.Docs,
                }
            );
        }
        db.SaveChanges();
    }

    private static void Delete(Scope scope, string name, string version)
    {
        var db = scope.Db;
        var (n, v) = (name, version);
        db.DocItems.Where(i => i.CrateName == n && i.Version == v).ExecuteDelete();
        db.DocCrates.Where(c => c.Name == n && c.Version == v).ExecuteDelete();
    }
}
