using System.Globalization;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Graph;

/// <summary>
/// The index's freshness for one answer. With <c>auto_index</c> every call walks and the stat
/// gate skips what did not change; without it a root with no rows is indexed once and edits
/// wait for <c>rtok graph index</c> or the watcher, and the answer leads with
/// <c>stale: N files pending (…)</c> until then.
/// </summary>
internal static class Stale
{
    private const int Listed = 5;

    /// <summary>Indexes <paramref name="root"/> as <c>auto_index</c> says and returns its
    /// canonical key.</summary>
    /// <exception cref="GraphRootException"><paramref name="root"/> is refused.</exception>
    public static string IndexFor(Ctx cx, string root)
    {
        var key = GraphRoot.Check(root);
        if (Indexer.Config(cx).AutoIndex)
        {
            Indexer.Run(cx, key);
        }
        else
        {
            Indexer.Ensure(cx, key);
        }

        return key;
    }

    /// <summary><paramref name="text"/> behind the pending banner, when there is one.</summary>
    public static string With(Ctx cx, string key, string text) => Banner(cx, key) + text;

    private static string Banner(Ctx cx, string key)
    {
        var config = Indexer.Config(cx);
        var watched = config.Watch is not "off" ? cx.Host.GraphWatchPending(key) : [];
        // With auto_index the walk already caught every edit but the watcher's queue.
        if (config.AutoIndex && watched.Count == 0)
        {
            return "";
        }

        var pending = cx
            .Host.SymbolPending(key, key)
            .Concat(cx.Host.GraphWatchPending(key))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (pending.Count == 0)
        {
            return "";
        }

        var listed = string.Join(", ", pending.Take(Listed));
        var tail = pending.Count > Listed ? ", …" : "";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"stale: {pending.Count} files pending ({listed}{tail})\n"
        );
    }
}
