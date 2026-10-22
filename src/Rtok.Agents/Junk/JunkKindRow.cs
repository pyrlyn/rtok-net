namespace Rtok.Agents.Junk;

/// <summary>One kind under an agent: what <c>clear</c> would remove, summed.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Class">Its class.</param>
/// <param name="Items">How many items.</param>
/// <param name="SizeBytes">Their bytes.</param>
public sealed record JunkKindRow(string Kind, string Class, int Items, long SizeBytes);
