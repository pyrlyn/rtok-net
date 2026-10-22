namespace Rtok.Plugins.Graph;

/// <summary>The pairs counted at <paramref name="Head"/>, kept until HEAD moves.</summary>
internal sealed record CochangeDoc(string Head, IReadOnlyList<CochangePair> Pairs);
