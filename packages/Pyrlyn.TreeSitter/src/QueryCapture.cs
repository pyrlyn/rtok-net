namespace Pyrlyn.TreeSitter;

/// <summary>A node captured by a query pattern.</summary>
/// <param name="Node">The captured node.</param>
/// <param name="Index">The capture index; its name is <see cref="Query.CaptureNames"/>[Index].</param>
public readonly record struct QueryCapture(Node Node, int Index);
