using Pyrlyn.FsKit;

namespace Rtok.Agents.Usage;

/// <summary>The session files under a host's folder, found the same way for every reader.</summary>
internal static class LogFiles
{
    /// <summary>
    /// Every <c>*.<paramref name="extension"/></c> under <paramref name="dir"/> (recursive) modified
    /// at or after <paramref name="cutoff"/>, sorted. Symlinks are followed, since a linked root or
    /// project folder is a real layout, but every entry is known by its real path, so an alias never
    /// yields the same file twice and a cycle ends. Real entries come before linked ones, so the path
    /// reported for a file is its real one.
    /// </summary>
    public static List<string> Find(string dir, DateTime cutoff, string extension)
    {
        var found = new List<string>();
        if (!FsPath.TryCanonicalize(dir, out var root))
        {
            return found;
        }
        var seenDirs = new HashSet<string>(StringComparer.Ordinal);
        var seenFiles = new HashSet<string>(StringComparer.Ordinal);
        var late = new List<(string Path, string Real)>();
        var real = new Stack<(string Path, string Real)>([(dir, root)]);
        var linked = new Stack<(string Path, string Real)>();
        var suffix = "." + extension;
        while (real.TryPop(out var next) || linked.TryPop(out next))
        {
            if (!seenDirs.Add(next.Real))
            {
                continue;
            }
            foreach (var entry in Entries(next.Path))
            {
                if (Resolve(entry, next.Real) is not { } child)
                {
                    continue;
                }
                var isLink = entry.LinkTarget is not null;
                if (child.IsDirectory)
                {
                    (isLink ? linked : real).Push((entry.FullName, child.Real));
                    continue;
                }
                if (
                    !entry.Name.EndsWith(suffix, StringComparison.Ordinal)
                    || entry.Name.Length == suffix.Length
                    || child.Modified < cutoff
                )
                {
                    continue;
                }
                if (isLink)
                {
                    late.Add((entry.FullName, child.Real));
                }
                else if (seenFiles.Add(child.Real))
                {
                    found.Add(entry.FullName);
                }
            }
        }
        // File links last, so a link never shadows the real path of the same file.
        found.AddRange(late.Where(l => seenFiles.Add(l.Real)).Select(l => l.Path));
        found.Sort(StringComparer.Ordinal);
        return found;
    }

    /// <summary>
    /// <paramref name="dirs"/> searched one after another, every result sorted. The readers that
    /// take a list of folders all start here.
    /// </summary>
    public static List<string> Find(IEnumerable<string> dirs, DateTime cutoff, string extension)
    {
        var found = dirs.SelectMany(d => Find(d, cutoff, extension)).ToList();
        found.Sort(StringComparer.Ordinal);
        return found;
    }

    /// <summary>The first folder of <paramref name="dirs"/> that exists and holds anything.</summary>
    public static string? FirstNonEmpty(IEnumerable<string> dirs) =>
        dirs.FirstOrDefault(d =>
        {
            try
            {
                return Directory.EnumerateFileSystemEntries(d).Any();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        });

    private static FileSystemInfo[] Entries(string dir)
    {
        try
        {
            return new DirectoryInfo(dir).GetFileSystemInfos();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>The entry itself, or the end of its link; null for a dangling or unreadable link.</summary>
    private static (bool IsDirectory, DateTime Modified, string Real)? Resolve(
        FileSystemInfo entry,
        string parent
    )
    {
        if (entry.LinkTarget is null)
        {
            return (
                entry is DirectoryInfo,
                entry.LastWriteTimeUtc,
                Path.Combine(parent, entry.Name)
            );
        }
        if (!FsPath.TryCanonicalize(entry.FullName, out var real))
        {
            return null;
        }
        try
        {
            return Directory.Exists(real) ? (true, default, real)
                : File.Exists(real) ? (false, File.GetLastWriteTimeUtc(real), real)
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
