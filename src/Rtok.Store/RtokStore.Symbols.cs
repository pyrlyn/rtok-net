using Microsoft.Data.Sqlite;
using Rtok.Plugin.Sdk;

namespace Rtok.Store;

/// <summary>
/// The graph plugin's symbol index (rtok-store <c>symbols.rs</c>): definitions and references
/// scoped per canonical root, so two repositories in one store never answer for each other.
/// Plain SQL on the store's connection: the walks, FTS5 and aggregates here are beyond what
/// EF's query precompiler takes (research.md F).
/// </summary>
public sealed partial class RtokStore : ISymbols
{
    private const string InsertSymbol = """
        INSERT INTO symbols (root, path, name, kind, line, end_line, start_byte, end_byte, is_def,
            scope, file_sha, mtime, size, content_hash, signature, doc)
        VALUES ($root, $path, $name, $kind, $line, $end_line, $start_byte, $end_byte, $is_def,
            $scope, $sha, $mtime, $size, $hash, $signature, $doc)
        """;

    public long SymbolCount(string root)
    {
        using var scope = Enter();
        return Scalar(scope, "SELECT COUNT(*) FROM symbols WHERE root = $root", root);
    }

    public long SymbolFileCount(string root)
    {
        using var scope = Enter();
        return Scalar(scope, "SELECT COUNT(DISTINCT path) FROM symbols WHERE root = $root", root);
    }

    public SymbolStat? GetSymbolStat(string root, string path)
    {
        using var scope = Enter();
        using var command = scope.Command(
            "SELECT file_sha, mtime, size FROM symbols WHERE root = $root AND path = $path LIMIT 1"
        );
        command.Parameters.AddWithValue("$root", root);
        command.Parameters.AddWithValue("$path", path);
        return Rows(command, r => new SymbolStat(r.GetString(0), r.GetInt64(1), r.GetInt64(2)))
            .FirstOrDefault();
    }

    public IReadOnlyDictionary<string, SymbolStat> SymbolStats(string root)
    {
        using var scope = Enter();
        using var command = scope.Command(
            "SELECT DISTINCT path, file_sha, mtime, size FROM symbols WHERE root = $root"
        );
        command.Parameters.AddWithValue("$root", root);
        var stats = new Dictionary<string, SymbolStat>(StringComparer.Ordinal);
        foreach (
            var (path, stat) in Rows(
                command,
                r => (r.GetString(0), new SymbolStat(r.GetString(1), r.GetInt64(2), r.GetInt64(3)))
            )
        )
        {
            stats[path] = stat;
        }

        return stats;
    }

    /// <summary>Hook-staled paths plus indexed paths whose stat no longer matches the disk;
    /// a file that cannot be stat'ed is not pending.</summary>
    public IReadOnlyList<string> SymbolPending(string root, string rootPath)
    {
        var pending = new SortedSet<string>(StringComparer.Ordinal);
        using (var scope = Enter())
        {
            using var command = scope.Command(
                "SELECT path FROM symbol_stale WHERE root = $root ORDER BY path"
            );
            command.Parameters.AddWithValue("$root", root);
            pending.UnionWith(Rows(command, r => r.GetString(0)));
        }

        foreach (var (path, stat) in SymbolStats(root))
        {
            var disk = SymbolStat.Stamp(Path.Combine(rootPath, path));
            if (disk != (stat.Mtime, stat.Size) && disk != (0, 0))
            {
                pending.Add(path);
            }
        }

        return [.. pending];
    }

    /// <summary>A new stat for a file whose content hashed the same: the rows stand, only the
    /// freshness key moves.</summary>
    public void TouchSymbols(string root, string path, long mtime, long size)
    {
        using var scope = EnterWrite();
        using var command = scope.Command(
            "UPDATE symbols SET mtime = $mtime, size = $size WHERE root = $root AND path = $path"
        );
        command.Parameters.AddWithValue("$root", root);
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$mtime", mtime);
        command.Parameters.AddWithValue("$size", size);
        command.ExecuteNonQuery();
        scope.Commit();
    }

    public int ReplaceSymbols(
        string root,
        string path,
        string fileSha,
        long mtime,
        long size,
        IReadOnlyList<SymbolRow> rows
    ) => ReplaceSymbolFiles(root, [new SymbolFile(path, fileSha, mtime, size, rows)]);

    /// <summary>Every file of the batch in one immediate transaction: each file's old rows go,
    /// its new rows land, and its stale mark clears. A tagless file keeps one nameless row so
    /// its sha stands and the next run skips it on the stat alone.</summary>
    public int ReplaceSymbolFiles(string root, IReadOnlyList<SymbolFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count == 0)
        {
            return 0;
        }

        using var scope = EnterWrite();
        using var delete = scope.Command("DELETE FROM symbols WHERE root = $root AND path = $path");
        using var unstale = scope.Command(
            "DELETE FROM symbol_stale WHERE root = $root AND path = $path"
        );
        using var insert = scope.Command(InsertSymbol);
        var inserted = 0;
        foreach (var file in files)
        {
            foreach (var command in (ReadOnlySpan<SqliteCommand>)[delete, unstale])
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$root", root);
                command.Parameters.AddWithValue("$path", file.Path);
                command.ExecuteNonQuery();
            }

            IReadOnlyList<SymbolRow> rows =
                file.Rows.Count == 0 ? [new SymbolRow("", "", 0, false, 0, "")] : file.Rows;
            foreach (var row in rows)
            {
                BindRow(insert, root, file, row);
                insert.ExecuteNonQuery();
            }

            inserted += file.Rows.Count;
        }

        scope.Commit();
        return inserted;
    }

    /// <summary>Drops every indexed file of <paramref name="root"/> not in
    /// <paramref name="keep"/> (all of them when it is empty); returns the rows removed.</summary>
    public int DeleteSymbolsMissing(string root, IReadOnlySet<string> keep)
    {
        ArgumentNullException.ThrowIfNull(keep);
        using var scope = EnterWrite();
        if (keep.Count == 0)
        {
            using var all = scope.Command("DELETE FROM symbols WHERE root = $root");
            all.Parameters.AddWithValue("$root", root);
            var gone = all.ExecuteNonQuery();
            scope.Commit();
            return gone;
        }

        using var paths = scope.Command("SELECT DISTINCT path FROM symbols WHERE root = $root");
        paths.Parameters.AddWithValue("$root", root);
        var missing = Rows(paths, r => r.GetString(0)).Where(p => !keep.Contains(p)).ToList();
        var removed = 0;
        using var delete = scope.Command("DELETE FROM symbols WHERE root = $root AND path = $path");
        foreach (var path in missing)
        {
            delete.Parameters.Clear();
            delete.Parameters.AddWithValue("$root", root);
            delete.Parameters.AddWithValue("$path", path);
            removed += delete.ExecuteNonQuery();
        }

        scope.Commit();
        return removed;
    }

    /// <summary>Drops the rows of one absolute file and marks it stale, under whichever root
    /// holds it: the caller does not know the root, so every separator split of the path is one
    /// indexed point delete. A same-named file of another root survives.</summary>
    public void MarkSymbolsStale(string absPath)
    {
        ArgumentNullException.ThrowIfNull(absPath);
        using var scope = EnterWrite();
        using var delete = scope.Command("DELETE FROM symbols WHERE root = $root AND path = $path");
        using var note = scope.Command(
            "INSERT OR IGNORE INTO symbol_stale (root, path) VALUES ($root, $path)"
        );
        for (var i = 0; i < absPath.Length; i++)
        {
            if (
                absPath[i] is not ('/' or '\\')
                || (absPath[i] is '\\' && !OperatingSystem.IsWindows())
            )
            {
                continue;
            }

            var root = absPath[..i];
            var rel = absPath[(i + 1)..].Replace('\\', '/');
            foreach (var command in (ReadOnlySpan<SqliteCommand>)[delete, note])
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$root", root);
                command.Parameters.AddWithValue("$path", rel);
            }

            if (delete.ExecuteNonQuery() > 0)
            {
                note.ExecuteNonQuery();
            }
        }

        scope.Commit();
    }

    public long? SymbolIndexedAt(string root)
    {
        using var scope = Enter();
        using var command = scope.Command("SELECT indexed_at FROM extractor WHERE root = $root");
        command.Parameters.AddWithValue("$root", root);
        return Rows(command, r => r.IsDBNull(0) ? (long?)null : r.GetInt64(0)).FirstOrDefault();
    }

    public void TouchSymbolIndexedAt(string root, long ts) =>
        Upsert(
            """
            INSERT INTO extractor (root, fingerprint, indexed_at) VALUES ($root, '', $value)
            ON CONFLICT (root) DO UPDATE SET indexed_at = excluded.indexed_at
            """,
            root,
            ts
        );

    public string? ExtractorFingerprint(string root) =>
        Text("SELECT fingerprint FROM extractor WHERE root = $root", root);

    public void SetExtractorFingerprint(string root, string fingerprint) =>
        Upsert(
            """
            INSERT INTO extractor (root, fingerprint) VALUES ($root, $value)
            ON CONFLICT (root) DO UPDATE SET fingerprint = excluded.fingerprint
            """,
            root,
            fingerprint
        );

    public string? FileRankGet(string root) =>
        Text("SELECT graph FROM file_rank WHERE root = $root", root);

    public void FileRankPut(string root, string graph) =>
        Upsert(
            """
            INSERT INTO file_rank (root, graph) VALUES ($root, $value)
            ON CONFLICT (root) DO UPDATE SET graph = excluded.graph
            """,
            root,
            graph
        );

    private static void BindRow(SqliteCommand insert, string root, SymbolFile file, SymbolRow row)
    {
        var p = insert.Parameters;
        p.Clear();
        p.AddWithValue("$root", root);
        p.AddWithValue("$path", file.Path);
        p.AddWithValue("$name", row.Name);
        p.AddWithValue("$kind", row.Kind);
        p.AddWithValue("$line", row.Line);
        p.AddWithValue("$end_line", row.EndLine);
        p.AddWithValue("$start_byte", row.StartByte);
        p.AddWithValue("$end_byte", row.EndByte);
        p.AddWithValue("$is_def", row.IsDef);
        p.AddWithValue("$scope", row.Scope);
        p.AddWithValue("$sha", file.Sha);
        p.AddWithValue("$mtime", file.Mtime);
        p.AddWithValue("$size", file.Size);
        p.AddWithValue("$hash", row.ContentHash);
        p.AddWithValue("$signature", row.Signature);
        p.AddWithValue("$doc", row.Doc);
    }

    private void Upsert(string sql, string root, object value)
    {
        using var scope = EnterWrite();
        using var command = scope.Command(sql);
        command.Parameters.AddWithValue("$root", root);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
        scope.Commit();
    }

    private string? Text(string sql, string root)
    {
        using var scope = Enter();
        using var command = scope.Command(sql);
        command.Parameters.AddWithValue("$root", root);
        return Rows(command, r => r.GetString(0)).FirstOrDefault();
    }

    private static long Scalar(in Scope scope, string sql, string root)
    {
        using var command = scope.Command(sql);
        command.Parameters.AddWithValue("$root", root);
        return Convert.ToInt64(
            command.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture
        );
    }

    private static List<T> Rows<T>(SqliteCommand command, Func<SqliteDataReader, T> row)
    {
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read())
        {
            rows.Add(row(reader));
        }

        return rows;
    }
}
