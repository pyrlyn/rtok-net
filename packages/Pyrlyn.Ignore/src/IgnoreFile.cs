namespace Pyrlyn.Ignore;

/// <summary>
/// The rules of one ignore file (<c>.gitignore</c>, <c>.ignore</c>, <c>info/exclude</c>, the
/// global excludes file, or a list of override lines), relative to <see cref="BaseDirectory"/>.
/// The last rule naming a path decides.
/// </summary>
public sealed class IgnoreFile
{
    private readonly IgnorePattern[] patterns;
    private readonly string prefix;

    private IgnoreFile(string baseDirectory, IgnorePattern[] patterns)
    {
        BaseDirectory = baseDirectory;
        prefix = baseDirectory.EndsWith('/') ? baseDirectory : baseDirectory + "/";
        this.patterns = patterns;
    }

    /// <summary>The directory the rules are relative to, <c>/</c>-separated.</summary>
    public string BaseDirectory { get; }

    /// <summary>The rules, in file order.</summary>
    public IReadOnlyList<IgnorePattern> Patterns => patterns;

    /// <summary>Rules from <paramref name="lines"/>, relative to <paramref name="baseDirectory"/>.</summary>
    public static IgnoreFile FromLines(
        string baseDirectory,
        IEnumerable<string> lines,
        bool ignoreCase = false
    )
    {
        ArgumentNullException.ThrowIfNull(baseDirectory);
        ArgumentNullException.ThrowIfNull(lines);
        return new IgnoreFile(
            Slashes(baseDirectory),
            [.. lines.Select(l => IgnorePattern.Parse(l, ignoreCase)).OfType<IgnorePattern>()]
        );
    }

    /// <summary>Rules from the text of an ignore file; a UTF-8 BOM is skipped.</summary>
    public static IgnoreFile Parse(string baseDirectory, string text, bool ignoreCase = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        return FromLines(baseDirectory, text.TrimStart('﻿').Split('\n'), ignoreCase);
    }

    /// <summary>
    /// Rules from the file at <paramref name="path"/>, relative to <paramref name="baseDirectory"/>;
    /// <see langword="null"/> when it does not exist or cannot be read, which git treats as no rules.
    /// </summary>
    public static IgnoreFile? Load(string path, string baseDirectory, bool ignoreCase = false)
    {
        try
        {
            return File.Exists(path)
                ? Parse(baseDirectory, File.ReadAllText(path), ignoreCase)
                : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>What these rules say about <paramref name="path"/>, an absolute or base-relative path.</summary>
    public IgnoreMatch Match(string path, bool isDirectory)
    {
        ArgumentNullException.ThrowIfNull(path);
        var p = Slashes(path);
        string relative;
        if (p.StartsWith(prefix, StringComparison.Ordinal))
        {
            relative = p[prefix.Length..];
        }
        else if (!Path.IsPathRooted(p))
        {
            relative = p;
        }
        else
        {
            return IgnoreMatch.None;
        }
        if (relative.Length == 0)
        {
            return IgnoreMatch.None;
        }
        for (var i = patterns.Length - 1; i >= 0; i--)
        {
            if (patterns[i].IsMatch(relative, isDirectory))
            {
                return patterns[i].Negated ? IgnoreMatch.Whitelist : IgnoreMatch.Ignore;
            }
        }
        return IgnoreMatch.None;
    }

    internal static string Slashes(string path) =>
        Path.DirectorySeparatorChar == '/' ? path : path.Replace('\\', '/');
}
