namespace Rtok.Proxy;

/// <summary>What one compressor job came to.</summary>
internal enum SummaryOutcome
{
    /// <summary>A summary for this archive id and version is already stored.</summary>
    Cached,

    /// <summary>Left to the extractive view; the detail says why.</summary>
    Skipped,

    /// <summary>The summary dropped must-keep spans and was not stored.</summary>
    Rejected,

    /// <summary>The upstream call or the store failed; nothing was stored.</summary>
    Failed,

    /// <summary>This job stored the summary.</summary>
    Stored,
}
