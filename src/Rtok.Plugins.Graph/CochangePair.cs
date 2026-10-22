namespace Rtok.Plugins.Graph;

/// <summary>Two files, <paramref name="A"/> before <paramref name="B"/> in ordinal order, and
/// the commits that touched both.</summary>
internal sealed record CochangePair(string A, string B, int Count);
