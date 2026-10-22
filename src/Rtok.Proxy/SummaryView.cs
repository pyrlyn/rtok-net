using Rtok.Store;

namespace Rtok.Proxy;

/// <summary>The block an archived tool result outside the live zone shows once the compressor
/// summarised it (rtok T385.9.2): <c>summary + expand(&lt;id&gt;)</c>.</summary>
internal static class SummaryView
{
    private const int ShortId = 12;

    /// <summary>The stored summary of <paramref name="archiveId"/> as the agent sees it. A summary
    /// never changes once stored, so the block is byte-stable from the turn it first appears.
    /// While none exists the id goes to <paramref name="offered"/> for the queue and the caller
    /// keeps its pointer; a view not shorter than the pointer is no saving and is not used.</summary>
    /// <param name="store">Where summaries are kept.</param>
    /// <param name="version">The compressor version (<see cref="CompressPrompt.Version"/>).</param>
    /// <param name="archiveId">The archived result.</param>
    /// <param name="pointerLength">The length of the pointer it would replace.</param>
    /// <param name="offered">Collects the ids that have no summary yet.</param>
    /// <returns>The view, or null to keep the pointer.</returns>
    public static string? Stored(
        RtokStore store,
        string version,
        string archiveId,
        int pointerLength,
        ICollection<string> offered
    )
    {
        if (store.GetArchiveSummary(archiveId, version) is not { } summary)
        {
            offered.Add(archiveId);
            return null;
        }
        var view = Render(archiveId, summary);
        return view.Length < pointerLength ? view : null;
    }

    /// <summary>The view of <paramref name="summary"/>.</summary>
    public static string Render(string archiveId, string summary) =>
        $"[compress {archiveId[..Math.Min(archiveId.Length, ShortId)]}]\ntype: tool_output\nsummary: {summary}\nexpand({archiveId})";
}
