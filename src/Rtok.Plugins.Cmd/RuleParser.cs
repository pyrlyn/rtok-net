using System.IO.Abstractions;
using Pyrlyn.Config.Layered;
using Tomlyn.Model;

namespace Rtok.Plugins.Cmd;

/// <summary>Reads rule files: strictly for user files and <c>rtok config validate</c>,
/// leniently for the built-in <c>rules/default.toml</c>.</summary>
internal static class RuleParser
{
    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
    {
        "max_lines",
        "head",
        "tail",
        "drop",
        "keep",
        "dedupe",
        "collapse_columns",
        "group",
        "json_items",
        "json_string",
    };

    /// <summary>The built-in rules embedded from <c>rules/default.toml</c>.</summary>
    public static List<Rule> Defaults()
    {
        using var stream = typeof(RuleParser).Assembly.GetManifestResourceStream(
            "rules/default.toml"
        )!;
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    /// <summary>
    /// One rules file, strictly (rtok T50.2): TOML syntax, table-only top level, known fields
    /// only, right types. Returns null and sets <paramref name="error"/> on the first problem.
    /// </summary>
    public static List<Rule>? ParseStrict(string text, out string error)
    {
        error = "";
        TomlTable doc;
        try
        {
            doc = TomlText.Parse(text);
        }
        catch (ConfigFileException e)
        {
            error = e.Message;
            return null;
        }

        var output = new List<Rule>();
        foreach (var (key, value) in doc)
        {
            if (value is not TomlTable table)
            {
                error = $"[{key}]: expected table";
                return null;
            }

            var rule = Strict(key, table, out error);
            if (rule is null)
            {
                return null;
            }

            output.Add(rule);
        }

        return output;
    }

    /// <summary>
    /// Malformed rules files among <paramref name="rulesPath"/> (when present) and every
    /// <c>*.toml</c> in <paramref name="rulesDir"/>, as <c>path: reason</c> lines. A missing file
    /// or dir is not an error: both default to the built-ins.
    /// </summary>
    public static List<string> IssuesIn(IFileSystem fs, string rulesPath, string rulesDir)
    {
        var errors = new List<string>();
        var files = new List<string>();
        if (fs.File.Exists(rulesPath))
        {
            files.Add(rulesPath);
        }

        files.AddRange(DropIns(fs, rulesDir));
        foreach (var path in files)
        {
            string text;
            try
            {
                text = fs.File.ReadAllText(path);
            }
            catch (FileNotFoundException)
            {
                continue;
            }
            catch (IOException e)
            {
                errors.Add($"{path}: cannot read ({e.Message})");
                continue;
            }
            catch (UnauthorizedAccessException e)
            {
                errors.Add($"{path}: cannot read ({e.Message})");
                continue;
            }

            if (ParseStrict(text, out var error) is null)
            {
                errors.Add($"{path}: {error}");
            }
        }

        return errors;
    }

    /// <summary>The <c>*.toml</c> files directly in <paramref name="dir"/>, in name order.</summary>
    public static List<string> DropIns(IFileSystem fs, string dir)
    {
        if (!fs.Directory.Exists(dir))
        {
            return [];
        }

        try
        {
            var files = fs
                .Directory.EnumerateFiles(dir)
                .Where(p =>
                    string.Equals(fs.Path.GetExtension(p), ".toml", StringComparison.Ordinal)
                )
                .ToList();
            files.Sort(StringComparer.Ordinal);
            return files;
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static Rule? Strict(string key, TomlTable table, out string error)
    {
        error = "";
        if (table.Keys.FirstOrDefault(f => !Fields.Contains(f)) is { } unknown)
        {
            error = $"[{key}].{unknown}: unknown field";
            return null;
        }

        if (
            !Num(table, key, "max_lines", 40, out var maxLines, ref error)
            || !Num(table, key, "head", 10, out var head, ref error)
            || !Num(table, key, "tail", 10, out var tail, ref error)
            || !Strs(table, key, "drop", out var drop, ref error)
            || !Strs(table, key, "keep", out var keep, ref error)
        )
        {
            return null;
        }

        var dedupe = ParseDedupe(
            table.TryGetValue("dedupe", out var d) ? d : null,
            out var dedupeError
        );
        if (dedupe is null)
        {
            error = $"[{key}].{dedupeError}";
            return null;
        }

        if (
            !Bool(table, key, "collapse_columns", out var collapse, ref error)
            || ParseGroup(table, key, ref error) is not { } group
            || !Num(table, key, "json_items", 20, out var jsonItems, ref error)
            || !Num(table, key, "json_string", 200, out var jsonString, ref error)
        )
        {
            return null;
        }

        return new Rule
        {
            MatchCmd = key,
            MaxLines = maxLines,
            Head = head,
            Tail = tail,
            Drop = drop,
            Keep = keep,
            Dedupe = dedupe.Value,
            CollapseColumns = collapse,
            Group = group,
            JsonItems = jsonItems,
            JsonString = jsonString,
        };
    }

    private static bool Bool(
        TomlTable table,
        string key,
        string field,
        out bool value,
        ref string error
    )
    {
        value = false;
        if (!table.TryGetValue(field, out var raw))
        {
            return true;
        }

        if (raw is bool b)
        {
            value = b;
            return true;
        }

        error = $"[{key}].{field}: expected bool";
        return false;
    }

    private static Group? ParseGroup(TomlTable table, string key, ref string error)
    {
        if (!table.TryGetValue("group", out var g))
        {
            return Group.Off;
        }

        switch (g)
        {
            case "dir":
                return Group.Dir;
            case "diag":
                return Group.Diag;
            default:
                error = $"[{key}].group: expected \"dir\" or \"diag\"";
                return null;
        }
    }

    private static bool Num(
        TomlTable table,
        string key,
        string field,
        uint fallback,
        out uint value,
        ref string error
    )
    {
        value = fallback;
        if (!table.TryGetValue(field, out var raw))
        {
            return true;
        }

        if (raw is long n && n is >= 0 and <= uint.MaxValue)
        {
            value = (uint)n;
            return true;
        }

        error = $"[{key}].{field}: expected integer ≥ 0";
        return false;
    }

    private static bool Strs(
        TomlTable table,
        string key,
        string field,
        out List<string> value,
        ref string error
    )
    {
        value = [];
        if (!table.TryGetValue(field, out var raw))
        {
            return true;
        }

        if (raw is TomlArray array && array.All(v => v is string))
        {
            value = [.. array.Cast<string>()];
            return true;
        }

        error = $"[{key}].{field}: expected array of strings";
        return false;
    }

    // A bool, or one of the strings "normalized", "true", "false"; absent is Adjacent.
    private static Dedupe? ParseDedupe(object? value, out string error)
    {
        error = "";
        switch (value)
        {
            case null:
                return Dedupe.Adjacent;
            case bool b:
                return b ? Dedupe.Adjacent : Dedupe.Off;
            case "normalized":
                return Dedupe.Normalized;
            case "true":
                return Dedupe.Adjacent;
            case "false":
                return Dedupe.Off;
            case string other:
                error = $"dedupe: expected bool or \"normalized\", got \"{other}\"";
                return null;
            default:
                error = "dedupe: expected bool or \"normalized\"";
                return null;
        }
    }

    // Lenient: a bad field takes its default and a non-table entry is skipped.
    private static List<Rule> Parse(string text)
    {
        TomlTable doc;
        try
        {
            doc = TomlText.Parse(text);
        }
        catch (ConfigFileException)
        {
            return [];
        }

        var output = new List<Rule>();
        foreach (var (key, value) in doc)
        {
            if (value is not TomlTable t)
            {
                continue;
            }

            uint N(string field, uint fallback) =>
                t.TryGetValue(field, out var v) && v is long n && n is >= 0 and <= uint.MaxValue
                    ? (uint)n
                    : fallback;
            List<string> S(string field) =>
                t.TryGetValue(field, out var v) && v is TomlArray a ? [.. a.OfType<string>()] : [];
            output.Add(
                new Rule
                {
                    MatchCmd = key,
                    MaxLines = N("max_lines", 40),
                    Head = N("head", 10),
                    Tail = N("tail", 10),
                    Drop = S("drop"),
                    Keep = S("keep"),
                    Dedupe =
                        ParseDedupe(t.TryGetValue("dedupe", out var d) ? d : null, out _)
                        ?? Dedupe.Adjacent,
                    CollapseColumns = t.TryGetValue("collapse_columns", out var c) && c is true,
                    Group = (t.TryGetValue("group", out var g) ? g : null) switch
                    {
                        "dir" => Group.Dir,
                        "diag" => Group.Diag,
                        _ => Group.Off,
                    },
                    JsonItems = N("json_items", 20),
                    JsonString = N("json_string", 200),
                }
            );
        }

        return output;
    }
}
