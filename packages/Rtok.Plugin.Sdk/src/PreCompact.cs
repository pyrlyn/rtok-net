namespace Rtok.Plugin.Sdk;

/// <summary>A compaction about to happen: the last chance to persist state.</summary>
/// <param name="Trigger"><c>manual</c> or <c>auto</c>.</param>
/// <param name="TranscriptPath">The transcript the host is about to compact.</param>
public sealed record PreCompact(string Trigger, string TranscriptPath);
