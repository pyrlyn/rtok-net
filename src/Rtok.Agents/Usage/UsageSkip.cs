namespace Rtok.Agents.Usage;

/// <summary>A host whose files exist but could not be read: named once, counted nowhere.</summary>
/// <param name="Host">The host id.</param>
/// <param name="Reason">Why: an unknown format, or a format the host does not document.</param>
/// <param name="Path">The first file or folder that was skipped.</param>
public sealed record UsageSkip(string Host, string Reason, string Path);
