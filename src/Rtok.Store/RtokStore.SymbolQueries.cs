using Microsoft.Data.Sqlite;

namespace Rtok.Store;

public sealed partial class RtokStore
{
    // SQLite's default variable limit is 999; a batch of names stays under it.
    private const int NameChunk = 500;

    /// <summary>Definitions of <paramref name="name"/>, by path then line.</summary>
    public IReadOnlyList<(string Path, string Kind, int Line, int EndLine)> SymbolDefs(
        string root,
        string name
    ) =>
        Query(
            """
            SELECT path, kind, line, end_line FROM symbols
            WHERE root = $root AND name = $name AND is_def = 1 ORDER BY path, line
            """,
            root,
            name,
            r => (r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetInt32(3))
        );

    /// <summary>References to <paramref name="name"/> collapsed to one row per calling
    /// definition: <c>(path, scope, count, first line)</c>, scope empty at file level.</summary>
    public IReadOnlyList<(string Path, string Kind, long Count, int Line)> SymbolRefGroups(
        string root,
        string name
    ) =>
        Query(
            """
            SELECT path, scope, COUNT(*), MIN(line) FROM symbols
            WHERE root = $root AND name = $name AND is_def = 0 AND kind != 'import'
            GROUP BY path, scope ORDER BY path, scope
            """,
            root,
            name,
            r => (r.GetString(0), r.GetString(1), r.GetInt64(2), r.GetInt32(3))
        );

    /// <summary>Callees of each definition of <paramref name="name"/>: a reference in the
    /// definition's file whose scope is the definition, with its first line.</summary>
    public IReadOnlyList<(string DefPath, int DefLine, string Callee, int FirstLine)> SymbolCallees(
        string root,
        string name
    ) =>
        Query(
            """
            SELECT d.path, d.line, r.name, MIN(r.line) AS first_line
            FROM symbols d JOIN symbols r
                ON r.root = d.root AND r.path = d.path AND r.is_def = 0 AND r.scope = d.name
            WHERE d.root = $root AND d.is_def = 1 AND d.name = $name AND r.name != ''
            GROUP BY d.path, d.line, r.name ORDER BY d.path, d.line, first_line, r.name
            """,
            root,
            name,
            r => (r.GetString(0), r.GetInt32(1), r.GetString(2), r.GetInt32(3))
        );

    /// <summary>Definitions no non-import reference names, by path then line.</summary>
    public IReadOnlyList<(string Path, string Name, string Kind, int Line)> SymbolDeadCandidates(
        string root
    ) =>
        Query(
            """
            SELECT d.path, d.name, d.kind, d.line FROM symbols d
            WHERE d.root = $root AND d.is_def = 1 AND d.name != '' AND NOT EXISTS (
                SELECT 1 FROM symbols r WHERE r.root = d.root AND r.name = d.name
                    AND r.is_def = 0 AND r.kind != 'import')
            ORDER BY d.path, d.line
            """,
            root,
            null,
            r => (r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3))
        );

    public IReadOnlySet<string> SymbolReferencedNames(string root, IReadOnlyList<string> names)
    {
        using var scope = Enter();
        return Chunked(
                scope,
                "SELECT DISTINCT name FROM symbols WHERE root = $root AND name IN {0} AND is_def = 0 AND kind != 'import'",
                root,
                names,
                r => r.GetString(0)
            )
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Definition names starting with <paramref name="prefix"/> (SQLite <c>LIKE</c>:
    /// ASCII case folded), most referenced first, ties by name.</summary>
    public IReadOnlyList<string> SymbolNamePrefix(string root, string prefix, int limit)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        var like =
            prefix
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal) + "%";
        using var scope = Enter();
        using var command = scope.Command(
            """
            SELECT s.name, (SELECT COUNT(*) FROM symbols r WHERE r.root = $root AND r.name = s.name
                    AND r.is_def = 0 AND r.kind != 'import') AS refs
            FROM symbols s
            WHERE s.root = $root AND s.is_def = 1 AND s.name != '' AND s.name LIKE $like ESCAPE '\'
            GROUP BY s.name ORDER BY refs DESC, s.name LIMIT $limit
            """
        );
        command.Parameters.AddWithValue("$root", root);
        command.Parameters.AddWithValue("$like", like);
        command.Parameters.AddWithValue("$limit", limit);
        return Rows(command, r => r.GetString(0));
    }

    public IReadOnlyList<(string Name, string Kind, int Line, int EndLine)> SymbolFileDefs(
        string root,
        string path
    ) =>
        Query(
            """
            SELECT name, kind, line, end_line FROM symbols
            WHERE root = $root AND path = $name AND is_def = 1 ORDER BY line
            """,
            root,
            path,
            r => (r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetInt32(3))
        );

    public IReadOnlyList<(string Name, int Line)> SymbolImports(string root, string path) =>
        Query(
            """
            SELECT name, line FROM symbols
            WHERE root = $root AND path = $name AND kind = 'import' AND is_def = 0 ORDER BY line
            """,
            root,
            path,
            r => (r.GetString(0), r.GetInt32(1))
        );

    public IReadOnlyList<(string Path, int Line)> SymbolImporters(string root, string moduleName) =>
        Query(
            """
            SELECT path, line FROM symbols
            WHERE root = $root AND name = $name AND kind = 'import' AND is_def = 0
            ORDER BY path, line
            """,
            root,
            moduleName,
            r => (r.GetString(0), r.GetInt32(1))
        );

    public IReadOnlyList<(string DefPath, string DefName)> SymbolImportFollow(
        string root,
        string name
    ) =>
        Query(
            """
            SELECT d.path, d.name FROM symbols i JOIN symbols d
                ON d.root = i.root AND d.path = i.path AND d.is_def = 1 AND d.name != ''
            WHERE i.root = $root AND i.name = $name AND i.kind = 'import' AND i.is_def = 0
            ORDER BY d.path, d.line
            """,
            root,
            name,
            r => (r.GetString(0), r.GetString(1))
        );

    /// <summary>Names by reference count with their first definition site (path, then line),
    /// most referenced first, ties by name. Import rows are not references.</summary>
    public IReadOnlyList<(string Name, long Refs, string Path, int Line)> SymbolTopRefs(
        string root,
        int limit
    )
    {
        using var scope = Enter();
        using var command = scope.Command(
            """
            SELECT d.name, (SELECT COUNT(*) FROM symbols r WHERE r.root = d.root AND r.name = d.name
                    AND r.is_def = 0 AND r.kind != 'import') AS refs, d.path, d.line
            FROM symbols d
            WHERE d.root = $root AND d.is_def = 1 AND d.name != '' AND NOT EXISTS (
                SELECT 1 FROM symbols e WHERE e.root = d.root AND e.name = d.name AND e.is_def = 1
                    AND (e.path < d.path OR (e.path = d.path AND e.line < d.line)))
            ORDER BY refs DESC, d.name LIMIT $limit
            """
        );
        command.Parameters.AddWithValue("$root", root);
        command.Parameters.AddWithValue("$limit", limit);
        return Rows(command, r => (r.GetString(0), r.GetInt64(1), r.GetString(2), r.GetInt32(3)));
    }

    /// <summary>One row per name, path and definition flag with its row count; imports and
    /// nameless rows left out.</summary>
    public IReadOnlyList<(string Name, string Path, bool IsDef, long Rows)> SymbolFileScan(
        string root
    ) =>
        Query(
            """
            SELECT name, path, is_def, COUNT(*) FROM symbols
            WHERE root = $root AND name != '' AND kind != 'import'
            GROUP BY name, path, is_def ORDER BY name, path, is_def
            """,
            root,
            null,
            r => (r.GetString(0), r.GetString(1), r.GetBoolean(2), r.GetInt64(3))
        );

    public Rtok.Plugin.Sdk.SymbolSpan? GetSymbolSpan(
        string root,
        string path,
        string name,
        string kind,
        int line
    )
    {
        using var scope = Enter();
        using var command = scope.Command(
            """
            SELECT start_byte, end_byte, content_hash, file_sha FROM symbols
            WHERE root = $root AND path = $path AND name = $name AND kind = $kind AND line = $line
                AND is_def = 1 LIMIT 1
            """
        );
        command.Parameters.AddWithValue("$root", root);
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$line", line);
        return Rows(
                command,
                r => new Rtok.Plugin.Sdk.SymbolSpan(
                    Math.Max(r.GetInt64(0), 0),
                    Math.Max(r.GetInt64(1), 0),
                    r.GetString(2),
                    r.GetString(3)
                )
            )
            .FirstOrDefault();
    }

    /// <summary>One read with <c>$root</c> and, when given, <c>$name</c> bound.</summary>
    private List<T> Query<T>(string sql, string root, string? name, Func<SqliteDataReader, T> row)
    {
        using var scope = Enter();
        using var command = scope.Command(sql);
        command.Parameters.AddWithValue("$root", root);
        if (name is not null)
        {
            command.Parameters.AddWithValue("$name", name);
        }

        return Rows(command, row);
    }

    /// <summary><paramref name="sql"/> once per chunk of <paramref name="names"/>, its
    /// <c>{0}</c> replaced by the chunk's parameter list.</summary>
    private static List<T> Chunked<T>(
        in Scope scope,
        string sql,
        string root,
        IReadOnlyList<string> names,
        Func<SqliteDataReader, T> row
    )
    {
        var rows = new List<T>();
        for (var start = 0; start < names.Count; start += NameChunk)
        {
            var count = Math.Min(NameChunk, names.Count - start);
            var list =
                "(" + string.Join(',', Enumerable.Range(0, count).Select(i => $"$n{i}")) + ")";
            using var command = scope.Command(
                string.Format(System.Globalization.CultureInfo.InvariantCulture, sql, list)
            );
            command.Parameters.AddWithValue("$root", root);
            for (var i = 0; i < count; i++)
            {
                command.Parameters.AddWithValue($"$n{i}", names[start + i]);
            }

            rows.AddRange(Rows(command, row));
        }

        return rows;
    }
}
