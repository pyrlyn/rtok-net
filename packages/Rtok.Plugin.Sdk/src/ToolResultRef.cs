namespace Rtok.Plugin.Sdk;

/// <summary>One wire-normalised tool result.</summary>
/// <param name="Id">Provider-stable result id: the key an archive decision is recorded
/// under.</param>
/// <param name="Content">The provider's result payload; rewrite it in place.</param>
/// <param name="Turn">How many user turns follow this result. The live tail is the expensive
/// part of a transcript to touch, so a plugin that shortens history leaves low turns
/// alone.</param>
public sealed record ToolResultRef(string Id, JsonSlot Content, int Turn);
