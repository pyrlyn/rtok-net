using System.Diagnostics.CodeAnalysis;
using Pyrlyn.FsKit.Native;

namespace Pyrlyn.FsKit;

/// <summary>
/// Path identity: the real path of an entry without the Windows <c>\\?\</c> prefix, Windows
/// case folding, a lexical join that needs no disk, and <see cref="Contain"/>, the one guard for
/// a path built from untrusted input. One copy, so a root one program drops is the same root
/// another refuses to walk.
/// </summary>
public static class FsPath
{
    private static readonly char[] Separators = OperatingSystem.IsWindows() ? ['\\', '/'] : ['/'];

    /// <summary>
    /// The real path of an existing entry, every symlink resolved, without a <c>\\?\</c> prefix.
    /// </summary>
    /// <exception cref="FileNotFoundException">Nothing is at <paramref name="path"/>.</exception>
    /// <exception cref="IOException">A symlink loop, or another OS error.</exception>
    public static string Canonicalize(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var real = Fs.TryRealPath(path, out var error);
        return real is null ? throw Fs.Exception(error, path) : Simplified(real);
    }

    /// <summary><see cref="Canonicalize"/> that answers false instead of throwing.</summary>
    public static bool TryCanonicalize(string path, [NotNullWhen(true)] out string? real)
    {
        real = string.IsNullOrEmpty(path) ? null : Fs.TryRealPath(path, out _);
        if (real is null)
        {
            return false;
        }
        real = Simplified(real);
        return true;
    }

    /// <summary>The real path of an existing entry; a missing one comes back <see cref="Simplified"/>.</summary>
    public static string Canon(string path) =>
        TryCanonicalize(path, out var real) ? real : Simplified(path);

    /// <summary>
    /// <paramref name="path"/> without a <c>\\?\</c> or <c>\\?\UNC\</c> prefix where dropping it
    /// keeps the meaning; no disk access. Unchanged off Windows.
    /// </summary>
    public static string Simplified(string path) =>
        OperatingSystem.IsWindows() ? StripVerbatim(path) : path;

    /// <summary>
    /// The prefix stays when the plain form would mean something else: 260 characters or more,
    /// a <c>.</c> or <c>..</c> component, a name ending in a dot or space, a reserved device
    /// name, or a forward slash, which a verbatim path takes literally.
    /// </summary>
    internal static string StripVerbatim(string path)
    {
        const string Unc = @"\\?\UNC\";
        const string Verbatim = @"\\?\";
        string plain;
        string body;
        if (path.StartsWith(Unc, StringComparison.OrdinalIgnoreCase))
        {
            body = path[Unc.Length..];
            plain = @"\\" + body;
        }
        else if (
            path.StartsWith(Verbatim, StringComparison.Ordinal)
            && path.Length >= 7
            && char.IsAsciiLetter(path[4])
            && path[5] == ':'
            && path[6] == '\\'
        )
        {
            body = path[7..];
            plain = path[4..];
        }
        else
        {
            return path;
        }
        if (plain.Length >= 260 || body.Contains('/', StringComparison.Ordinal))
        {
            return path;
        }
        foreach (var part in body.Split('\\'))
        {
            if (
                part is "." or ".."
                || part.EndsWith('.')
                || part.EndsWith(' ')
                || IsReservedName(part)
            )
            {
                return path;
            }
        }
        return plain;
    }

    private static bool IsReservedName(string part)
    {
        var stem = part.Split('.')[0].TrimEnd(' ');
        if (stem.Length == 3)
        {
            return stem.ToUpperInvariant() is "CON" or "PRN" or "AUX" or "NUL";
        }
        if (stem.Length == 4 && stem[3] is >= '0' and <= '9' or '¹' or '²' or '³')
        {
            return stem[..3].ToUpperInvariant() is "COM" or "LPT";
        }
        return false;
    }

    /// <summary>
    /// Lexical join of <paramref name="path"/> onto <paramref name="root"/>: <c>..</c> pops,
    /// <c>.</c> drops, and an absolute <paramref name="path"/> replaces <paramref name="root"/>. No
    /// disk access, so a missing path still normalises.
    /// </summary>
    /// <remarks>
    /// Deliberately not clamped to <paramref name="root"/>: a linked worktree's
    /// <c>gitdir: ../../main/.git/…</c> legitimately climbs above it. Confinement is
    /// <see cref="Contain"/>'s job.
    /// </remarks>
    public static string Normalize(string root, string path)
    {
        var (start, parts) = Path.IsPathRooted(path)
            ? Split(path, keepDots: true)
            : Split(root, keepDots: true);
        var stack = new List<string>();
        Push(stack, parts);
        if (!Path.IsPathRooted(path))
        {
            Push(stack, Split(path, keepDots: true).Parts);
        }
        return Join(start, stack);

        static void Push(List<string> stack, string[] parts)
        {
            foreach (var part in parts)
            {
                if (string.Equals(part, "..", StringComparison.Ordinal))
                {
                    if (stack.Count > 0)
                    {
                        stack.RemoveAt(stack.Count - 1);
                    }
                }
                else if (!string.Equals(part, ".", StringComparison.Ordinal))
                {
                    stack.Add(part);
                }
            }
        }
    }

    /// <summary>
    /// Where <paramref name="path"/> lands: symlinks resolved through its deepest existing
    /// ancestor, the rest applied lexically. A path that does not exist yet is judged by where it
    /// would land.
    /// </summary>
    public static string Resolved(string path)
    {
        var (root, parts) = Split(path, keepDots: true);
        for (var k = parts.Length; k >= 0; k--)
        {
            var ancestor = Join(root, parts.AsSpan(0, k));
            if (ancestor.Length == 0)
            {
                ancestor = ".";
            }
            if (TryCanonicalize(ancestor, out var real))
            {
                // The tail does not exist, so it holds no symlink and its `..` is safe to apply lexically.
                return Normalize(real, Join("", parts.AsSpan(k)));
            }
        }
        return Canon(path);
    }

    /// <summary>
    /// <paramref name="path"/> (a relative one starts at <paramref name="root"/>) as the place it
    /// lands, with symlinks followed before any <c>..</c>. A lexical <c>..</c> check alone misses a
    /// symlink inside the root that points out of it.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">It lands outside <paramref name="root"/>.</exception>
    public static string Contain(string root, string path)
    {
        // Joined, not normalized: `link/..` is the link target's parent, which only the OS knows.
        var landed = Resolved(Path.Combine(root, path));
        if (StartsWith(landed, Resolved(root)))
        {
            return landed;
        }
        throw new UnauthorizedAccessException($"{path} resolves to {landed}, outside {root}");
    }

    /// <summary>Whether two paths name one existing entry (<c>/var</c> and <c>/private/var</c>, <c>RUNNER~1</c>).</summary>
    /// <remarks>A path that does not resolve matches only its own spelling.</remarks>
    public static bool SameFile(string a, string b) =>
        string.Equals(a, b, StringComparison.Ordinal)
        || (
            TryCanonicalize(a, out var x)
            && TryCanonicalize(b, out var y)
            && string.Equals(x, y, StringComparison.Ordinal)
        );

    /// <summary>The same path, Windows case and a <c>\\?\</c> prefix included; no disk access.</summary>
    public static bool SamePath(string a, string b) => SamePath(a, b, OperatingSystem.IsWindows());

    internal static bool SamePath(string a, string b, bool fold)
    {
        var (ra, pa) = Split(Simplified(a), keepDots: false);
        var (rb, pb) = Split(Simplified(b), keepDots: false);
        return pa.Length == pb.Length && Same(ra, rb, fold) && Prefix(pa, pb, fold);
    }

    /// <summary>
    /// <paramref name="path"/> is <paramref name="root"/> or under it, whole components only. On
    /// Windows the check folds case and a <c>\\?\</c> prefix is not a different directory.
    /// </summary>
    public static bool StartsWith(string path, string root) =>
        StartsWith(path, root, OperatingSystem.IsWindows());

    internal static bool StartsWith(string path, string root, bool fold)
    {
        if (root.Length == 0)
        {
            return false;
        }
        var (rp, pp) = Split(Simplified(path), keepDots: false);
        var (rr, pr) = Split(Simplified(root), keepDots: false);
        return pr.Length <= pp.Length && Same(rp, rr, fold) && Prefix(pp, pr, fold);
    }

    /// <summary>
    /// <paramref name="suffix"/> names the trailing components of <paramref name="path"/>
    /// (<c>src/a.cs</c> in <c>/repo/src/a.cs</c>), whole components only, case folded on Windows.
    /// </summary>
    public static bool EndsWith(string path, string suffix) =>
        EndsWith(path, suffix, OperatingSystem.IsWindows());

    internal static bool EndsWith(string path, string suffix, bool fold)
    {
        if (suffix.Length == 0)
        {
            return false;
        }
        if (Path.IsPathRooted(suffix))
        {
            return SamePath(path, suffix, fold);
        }
        var pp = Split(path, keepDots: false).Parts;
        var ps = Split(suffix, keepDots: false).Parts;
        return ps.Length <= pp.Length && Prefix(pp.AsSpan(pp.Length - ps.Length), ps, fold);
    }

    /// <summary>
    /// <paramref name="path"/> relative to <paramref name="prefix"/>, or null when it is not under
    /// it; <c>""</c> for the prefix itself. Case and a <c>\\?\</c> prefix still match on Windows.
    /// </summary>
    public static string? StripPrefix(string path, string prefix) =>
        StripPrefix(path, prefix, OperatingSystem.IsWindows());

    internal static string? StripPrefix(string path, string prefix, bool fold)
    {
        if (!StartsWith(path, prefix, fold))
        {
            return null;
        }
        var skip = Split(Simplified(prefix), keepDots: false).Parts.Length;
        return Join("", Split(Simplified(path), keepDots: false).Parts.AsSpan(skip));
    }

    /// <summary>
    /// Whether <paramref name="root"/> is the filesystem root or <paramref name="home"/>, which
    /// no index or walk may use as a project root.
    /// </summary>
    public static bool IsUnwalkableRoot(string root, string? home)
    {
        var real = Canon(root);
        return Path.GetDirectoryName(real) is null
            || (home is not null && SamePath(Canon(home), real));
    }

    private static (string Root, string[] Parts) Split(string path, bool keepDots)
    {
        var root = Path.GetPathRoot(path) ?? "";
        if (OperatingSystem.IsWindows())
        {
            root = root.Replace('/', '\\');
        }
        var parts = path[root.Length..].Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        return (
            root,
            keepDots
                ? parts
                : Array.FindAll(parts, p => !string.Equals(p, ".", StringComparison.Ordinal))
        );
    }

    private static string Join(string root, ReadOnlySpan<string> parts)
    {
        if (parts.IsEmpty)
        {
            return root;
        }
        var separator = Path.DirectorySeparatorChar;
        var rest = string.Join(separator, parts!);
        return root.Length == 0 || root[^1] == separator || root[^1] == '/'
            ? root + rest
            : root + separator + rest;
    }

    private static string Join(string root, List<string> parts) =>
        Join(root, parts.ToArray().AsSpan());

    private static bool Same(string a, string b, bool fold) =>
        string.Equals(a, b, fold ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool Prefix(ReadOnlySpan<string> path, ReadOnlySpan<string> prefix, bool fold)
    {
        for (var i = 0; i < prefix.Length; i++)
        {
            if (!Same(path[i], prefix[i], fold))
            {
                return false;
            }
        }
        return true;
    }
}
