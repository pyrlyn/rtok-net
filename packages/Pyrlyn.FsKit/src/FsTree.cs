using System.Diagnostics;
using Pyrlyn.FsKit.Native;

namespace Pyrlyn.FsKit;

/// <summary>Whole trees.</summary>
public static class FsTree
{
    /// <summary>
    /// Disk usage of <paramref name="path"/>, not its apparent size: allocated blocks, so sparse
    /// files are not over-counted; a file with several hard links once; and on APFS, the clones of
    /// one file share their blocks once, each further clone adding only its private bytes. A
    /// symlink costs itself and is never followed, so a link out of the tree cannot pull foreign
    /// bytes in.
    /// </summary>
    /// <param name="path">A file or directory; nothing there is an empty usage.</param>
    /// <param name="timeout">
    /// Stop after this long (a huge cache on a network volume must not hang a listing); null walks to the end.
    /// </param>
    public static TreeUsage Usage(string path, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var walk = new Walk(timeout);
        var pending = new Stack<string>();
        pending.Push(path);
        while (pending.Count > 0)
        {
            if (walk.Expired())
            {
                walk.TimedOut = true;
                break;
            }
            var entry = pending.Pop();
            if (walk.Visit(entry))
            {
                walk.Children(entry, pending);
            }
        }
        return new TreeUsage(walk.Bytes, walk.Files, walk.Denied, walk.TimedOut);
    }

    /// <summary>
    /// The newest file mtime under <paramref name="path"/> and the <c>CACHEDIR.TAG</c> caches in
    /// it. A symlink is never followed (it counts its own mtime), and a nested checkout (a
    /// directory below the root holding <c>.git</c>) is skipped: it is its own tree.
    /// </summary>
    /// <param name="path">A file or directory; nothing there is an empty answer.</param>
    /// <param name="timeout">Stop after this long; null walks to the end.</param>
    public static TreeAges Ages(string path, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var walk = new AgeWalk(timeout);
        FileSystemInfo root = new FileInfo(path);
        if (!root.Exists)
        {
            root = new DirectoryInfo(path);
            if (!root.Exists)
            {
                return new TreeAges(null, false, false, []);
            }
        }
        walk.Run(root);
        return walk.Result();
    }

    private sealed class AgeWalk(TimeSpan? timeout)
    {
        private readonly long started = Stopwatch.GetTimestamp();
        private readonly List<(string Path, DateTime? Newest, bool Incomplete)> caches = [];
        private DateTime? newest;
        private bool incomplete;
        private bool timedOut;

        public TreeAges Result() =>
            new(
                newest,
                incomplete,
                timedOut,
                [.. caches.Select(c => new TaggedCache(c.Path, c.Newest, c.Incomplete))]
            );

        public void Run(FileSystemInfo root)
        {
            if (root is not DirectoryInfo dir || dir.LinkTarget is not null)
            {
                File(root, null);
                return;
            }
            var pending = new Stack<(DirectoryInfo Dir, int? Cache, bool Root)>();
            pending.Push((dir, null, true));
            while (pending.Count > 0)
            {
                if (timeout is { } t && Stopwatch.GetElapsedTime(started) >= t)
                {
                    timedOut = true;
                    return;
                }
                var (d, cache, isRoot) = pending.Pop();
                if (Entries(d, cache) is not { } entries)
                {
                    continue;
                }
                if (!isRoot && entries.Any(e => e.Name is ".git"))
                {
                    continue;
                }
                if (
                    cache is null
                    && entries.Any(e =>
                        string.Equals(e.Name, CacheDirTag.FileName, StringComparison.Ordinal)
                    )
                    && CacheDirTag.IsCacheDir(d.FullName)
                )
                {
                    caches.Add((d.FullName, null, false));
                    cache = caches.Count - 1;
                }
                foreach (var e in entries)
                {
                    if (
                        e is DirectoryInfo sub
                        && !e.Attributes.HasFlag(FileAttributes.ReparsePoint)
                    )
                    {
                        pending.Push((sub, cache, false));
                    }
                    else
                    {
                        File(e, cache);
                    }
                }
            }
        }

        private FileSystemInfo[]? Entries(DirectoryInfo dir, int? cache)
        {
            try
            {
                return dir.GetFileSystemInfos(
                    "*",
                    new EnumerationOptions
                    {
                        AttributesToSkip = FileAttributes.None,
                        IgnoreInaccessible = false,
                        RecurseSubdirectories = false,
                    }
                );
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Missed(cache);
                return null;
            }
        }

        private void File(FileSystemInfo file, int? cache)
        {
            DateTime modified;
            try
            {
                file.Refresh();
                if (!file.Exists && file.LinkTarget is null)
                {
                    return;
                }
                modified = file.LastWriteTimeUtc;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Missed(cache);
                return;
            }
            newest = Max(newest, modified);
            if (cache is { } i)
            {
                caches[i] = caches[i] with { Newest = Max(caches[i].Newest, modified) };
            }
        }

        private void Missed(int? cache)
        {
            incomplete = true;
            if (cache is { } i)
            {
                caches[i] = caches[i] with { Incomplete = true };
            }
        }

        private static DateTime Max(DateTime? a, DateTime b) => a is { } x && x >= b ? x : b;
    }

    private sealed class Walk(TimeSpan? timeout)
    {
        private readonly long started = Stopwatch.GetTimestamp();
        private readonly HashSet<(ulong, ulong)> links = [];
        private readonly HashSet<(ulong, ulong)> clones = [];

        public long Bytes;
        public long Files;
        public bool Denied;
        public bool TimedOut;

        public bool Expired() => timeout is { } t && Stopwatch.GetElapsedTime(started) >= t;

        /// <summary>Count one entry; true when it is a directory to descend into.</summary>
        public bool Visit(string path)
        {
            if (!Fs.TryLStat(path, out var stat, out var error))
            {
                if (error == 0)
                {
                    return Fallback(path);
                }
                Denied |= Fs.IsDenied(error);
                return false;
            }
            if (stat.Kind != StatKind.Directory)
            {
                Files++;
            }
            if (stat.Links >= 2 && !links.Add((stat.Device, stat.FileId)))
            {
                return false;
            }
            Bytes += CloneShare(path, stat);
            return stat.Kind == StatKind.Directory;
        }

        /// <summary>The first clone of a family pays its blocks; each later one only what is its own.</summary>
        private long CloneShare(string path, Stat stat)
        {
            if (
                stat.Kind == StatKind.File
                && Unix.TryCloneInfo(path, out var clone)
                && clone.RefCount > 1
                && !clones.Add((stat.Device, clone.CloneId))
            )
            {
                return Math.Min(clone.PrivateSize, stat.Allocated);
            }
            return stat.Allocated;
        }

        /// <summary>A platform without a stat layout: apparent sizes, every link counted.</summary>
        private bool Fallback(string path)
        {
            var info = new FileInfo(path);
            if (info.Exists)
            {
                Files++;
                Bytes += info.LinkTarget is null ? info.Length : 0;
                return false;
            }
            return Directory.Exists(path) && new DirectoryInfo(path).LinkTarget is null;
        }

        public void Children(string dir, Stack<string> pending)
        {
            try
            {
                var options = new EnumerationOptions
                {
                    AttributesToSkip = FileAttributes.None,
                    IgnoreInaccessible = false,
                    RecurseSubdirectories = false,
                };
                foreach (var child in Directory.EnumerateFileSystemEntries(dir, "*", options))
                {
                    pending.Push(child);
                }
            }
            catch (UnauthorizedAccessException)
            {
                Denied = true;
            }
            catch (IOException) { }
        }
    }
}
