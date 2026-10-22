namespace Rtok.Store;

/// <summary>One hook, mcp or proxy call as the report times it.</summary>
/// <param name="Surface">hook, mcp or proxy.</param>
/// <param name="Name">The hook event or tool name, when the call recorded one.</param>
/// <param name="Ms">The latency, when the call recorded one.</param>
/// <param name="Measured">Whether a <c>measurements</c> row points at the call.</param>
public sealed record CallTiming(string Surface, string? Name, double? Ms, bool Measured);
