namespace Rtok.Store;

/// <summary>
/// Whether an import specifier names a repository file (rtok T368): the file's path segments,
/// stem and <c>mod</c>/<c>lib</c>/<c>index</c> dropped, must prefix the specifier's segments.
/// Hyphens match underscores. A specifier and a path share no column, so this is not a join.
/// </summary>
internal static class ImportKeys
{
    private static readonly char[] SpecSeparators = ['/', '\\', ':', '.'];
    private static readonly char[] PathSeparators = ['/', '\\'];

    /// <summary>The segments of a specifier, <c>crate</c>, <c>self</c>, <c>super</c> and
    /// <c>package</c> left out.</summary>
    public static List<string> ImportSegs(string spec) =>
        spec.Split(SpecSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Where(s => s is not ("crate" or "self" or "super" or "package"))
            .Select(s => s.Replace('-', '_'))
            .ToList();

    /// <summary>The segments a file is imported by: no extension, no trailing
    /// <c>mod</c>/<c>lib</c>/<c>index</c>, no <c>src</c> or <c>crates</c>.</summary>
    public static List<string> FileKey(string file)
    {
        var parts = file.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parts.Count > 0 && parts[^1].LastIndexOf('.') is var dot and >= 0)
        {
            parts[^1] = parts[^1][..dot];
        }

        if (parts.Count > 0 && parts[^1] is "mod" or "lib" or "index")
        {
            parts.RemoveAt(parts.Count - 1);
        }

        return parts
            .Where(p => p is not ("src" or "crates"))
            .Select(p => p.Replace('-', '_'))
            .ToList();
    }

    /// <summary>The segments of a specifier that is a path; <see langword="null"/> for a bare
    /// identifier or an empty path.</summary>
    public static List<string>? ImportPathSegs(string spec)
    {
        if (spec.IndexOfAny(SpecSeparators) < 0)
        {
            return null;
        }

        var segs = ImportSegs(spec);
        return segs.Count == 0 ? null : segs;
    }

    public static bool KeyPrefixesImport(List<string> file, List<string> import)
    {
        if (file.Count == 0 || file.Count > import.Count)
        {
            return false;
        }

        for (var i = 0; i < file.Count; i++)
        {
            if (!string.Equals(file[i], import[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public static bool ImportMatchesFile(string spec, string file) =>
        ImportPathSegs(spec) is { } import && KeyPrefixesImport(FileKey(file), import);
}
