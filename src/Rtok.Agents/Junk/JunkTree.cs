using Pyrlyn.FsKit;

namespace Rtok.Agents.Junk;

/// <summary>The file-system answers every junk kind asks: sizes, ages, links, real paths.</summary>
internal static class JunkTree
{
    public static long Bytes(string path, TimeSpan limit) => FsTree.Usage(path, limit).Bytes;

    /// <summary>The newest file mtime under <paramref name="path"/> (a folder's own mtime is not use); <c>Done</c> false when the walk did not finish, so an unknown age never reads as old.</summary>
    public static (bool Done, DateTime? Newest) Newest(string path)
    {
        if (!Exists(path))
        {
            return (false, null);
        }
        if (!IsDirectory(path))
        {
            return (true, new FileInfo(path).LastWriteTimeUtc);
        }
        var ages = FsTree.Ages(path, JunkContext.ScanLimit);
        return ages.TimedOut ? (false, null) : (true, ages.Newest);
    }

    public static bool Exists(string path) =>
        File.Exists(path) || Directory.Exists(path) || IsSymlink(path);

    public static bool IsSymlink(string path)
    {
        try
        {
            return new FileInfo(path).LinkTarget is not null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>A real directory: a link to one is not.</summary>
    public static bool IsDirectory(string path) => Directory.Exists(path) && !IsSymlink(path);

    /// <summary>A real file: a link to one is not.</summary>
    public static bool IsFile(string path) => File.Exists(path) && !IsSymlink(path);

    public static string Real(string path) => FsPath.Canon(path);

    /// <summary>Whether <paramref name="path"/> is <paramref name="root"/> or inside it, by whole components.</summary>
    public static bool Under(string path, string root)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var trimmed = root.TrimEnd('/', '\\');
        if (trimmed.Length == 0)
        {
            return path.StartsWith(root, comparison);
        }
        return path.Equals(trimmed, comparison)
            || (
                path.StartsWith(trimmed, comparison)
                && path.Length > trimmed.Length
                && path[trimmed.Length] is '/' or '\\'
            );
    }

    /// <summary>The entries of a directory, or none when it cannot be read.</summary>
    public static string[] Children(string dir)
    {
        try
        {
            return Directory.Exists(dir) ? Directory.GetFileSystemEntries(dir) : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary><paramref name="paths"/> without any that sits inside another one, so a directory is walked once.</summary>
    internal static List<string> Outermost(IEnumerable<string> paths)
    {
        var all = paths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return
        [
            .. all.Where(d =>
                !all.Exists(o => !string.Equals(o, d, StringComparison.Ordinal) && Under(d, o))
            ),
        ];
    }

    /// <summary>The sum over the paths not inside another listed one, so a nested folder is not added on top of its parent.</summary>
    public static long TopLevelBytes(IReadOnlyList<(string Path, long Bytes)> folders) =>
        folders
            .Where(f =>
                !folders.Any(o =>
                    !string.Equals(o.Path, f.Path, StringComparison.Ordinal)
                    && Under(f.Path, o.Path)
                )
            )
            .Sum(f => f.Bytes);
}
