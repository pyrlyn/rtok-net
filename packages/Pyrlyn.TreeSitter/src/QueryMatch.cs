namespace Pyrlyn.TreeSitter;

/// <summary>One match of a query pattern, with its captures in pattern order.</summary>
public sealed class QueryMatch
{
    internal QueryMatch(int id, int patternIndex, QueryCapture[] captures)
    {
        Id = id;
        PatternIndex = patternIndex;
        Captures = captures;
    }

    /// <summary>The match id, unique within one run of a cursor.</summary>
    public int Id { get; }

    /// <summary>The pattern that matched.</summary>
    public int PatternIndex { get; }

    /// <summary>The captures.</summary>
    public IReadOnlyList<QueryCapture> Captures { get; }

    /// <summary>The nodes captured as <paramref name="captureIndex"/>.</summary>
    public IEnumerable<Node> NodesForCapture(int captureIndex)
    {
        foreach (var capture in Captures)
        {
            if (capture.Index == captureIndex)
            {
                yield return capture.Node;
            }
        }
    }
}
