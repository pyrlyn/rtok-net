using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Graph;

/// <summary>The commit a root was last indexed at, and the line an answer leads with once
/// HEAD has moved past it.</summary>
internal static class IndexedHead
{
    private static string Key(string root) => "head:" + root;

    /// <summary>Remembers HEAD for the canonical <paramref name="root"/>; no repository keeps
    /// nothing.</summary>
    public static void Remember(Ctx cx, string root)
    {
        if (Git.Head(root) is { } head)
        {
            cx.Host.PluginStateSet("graph", Key(root), head);
        }
    }

    /// <summary><c>index &lt;stored&gt; head &lt;live&gt;\n</c> when the indexed commit is not
    /// HEAD; empty when nothing is stored, git fails or the two match, so the answer stays
    /// byte-identical.</summary>
    public static string StalePrefix(Ctx cx, string root)
    {
        if (cx.Host.PluginStateGet("graph", Key(root)) is not { Length: > 0 } stored)
        {
            return "";
        }

        return Git.Head(root) is { } live && !string.Equals(stored, live, StringComparison.Ordinal)
            ? $"index {stored} head {live}\n"
            : "";
    }
}
