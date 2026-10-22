using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Graph;

/// <summary>
/// The debounce of one watched root (rtok <c>graph/watch.rs</c>): source paths wait in
/// <c>pending</c> until <see cref="Quiet"/> passes without a relevant event, then only they are
/// re-indexed. A directory event, an OS overflow or more than <see cref="PendingCap"/> paths
/// turn the next run into a full walk. The pending set is published for the stale banner.
/// </summary>
internal sealed class WatchState(Ctx cx, Matcher matcher)
{
    public static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(250);

    public const int PendingCap = 1024;

    private readonly HashSet<string> _pending = new(StringComparer.Ordinal);
    private string[] _published = [];

    /// <summary>When the last relevant event arrived, or the last run ended.</summary>
    public DateTimeOffset Last { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The next run walks the whole root.</summary>
    public bool Rescan { get; private set; }

    public IReadOnlyCollection<string> Pending => _pending;

    private int _runs;

    /// <summary>Index runs so far; read from any thread.</summary>
    public int Runs => Volatile.Read(ref _runs);

    /// <summary>Takes one event in; <see langword="true"/> when it restarts the quiet window.
    /// Git's own files, paths the walk skips and existing files no grammar parses are
    /// ignored: a store or log under the root must not keep postponing the run.</summary>
    public bool Absorb(WatchEvent ev, DateTimeOffset now)
    {
        var touched = ev.Rescan ? Overflow() : ev.Path is { } path && Path(path);
        if (_pending.Count > PendingCap)
        {
            touched = Overflow();
        }

        if (touched)
        {
            Last = now;
        }

        return touched;
    }

    /// <summary>Runs the index once the window is quiet and something is pending;
    /// <see langword="true"/> when it ran. A failed run is logged and dropped: the next
    /// call's own index catches up.</summary>
    public bool Settle(DateTimeOffset now)
    {
        if (now - Last < Quiet || (!Rescan && _pending.Count == 0))
        {
            return false;
        }

        try
        {
            if (Rescan)
            {
                Indexer.Run(cx, matcher.Root);
            }
            else
            {
                Indexer.RunChanged(cx, matcher.Root, _pending);
            }
        }
#pragma warning disable CA1031 // A background run never takes the server down.
        catch (Exception e)
        {
            cx.Host.Log("warn", "graph", "watch", $"{matcher.Root}: {e.Message}");
        }
#pragma warning restore CA1031

        Interlocked.Increment(ref _runs);
        Last = now;
        _pending.Clear();
        Rescan = false;
        Publish();
        return true;
    }

    /// <summary>Publishes the pending set, root-relative and sorted, when it changed.</summary>
    public void Publish()
    {
        string[] paths =
        [
            .. _pending.Select(matcher.Relative).OfType<string>().Order(StringComparer.Ordinal),
        ];
        if (paths.AsSpan().SequenceEqual(_published))
        {
            return;
        }

        _published = paths;
        cx.Host.PublishGraphWatchPending(matcher.Root, paths);
    }

    private bool Path(string path)
    {
        var full = System.IO.Path.GetFullPath(path, matcher.Root);
        if (matcher.Relative(full) is not { } relative || IsGit(relative))
        {
            return false;
        }

        var isDirectory = Directory.Exists(full);
        if (!matcher.IsWalked(full, isDirectory))
        {
            return false;
        }

        if (!isDirectory && matcher.Indexable(full))
        {
            _pending.Add(full);
            return true;
        }

        // A directory, or a vanished path with no extension that was one: its children may
        // have gone with it, which only a walk finds.
        return (
                isDirectory || (!File.Exists(full) && System.IO.Path.GetExtension(full).Length == 0)
            ) && Overflow();
    }

    private bool Overflow()
    {
        Rescan = true;
        _pending.Clear();
        return true;
    }

    private static bool IsGit(string relative) =>
        relative.Split('/').Contains(".git", StringComparer.Ordinal);
}
