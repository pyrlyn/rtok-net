using Pyrlyn.TreeSitter.Native;

namespace Pyrlyn.TreeSitter;

/// <summary>Runs queries over trees. Not thread-safe: one cursor per thread.</summary>
public sealed class QueryCursor : IDisposable
{
    private readonly QueryCursorHandle _handle = new();

    /// <summary>
    /// The most in-progress matches the cursor keeps; past it the oldest are dropped and
    /// <see cref="DidExceedMatchLimit"/> turns true. Unlimited by default.
    /// </summary>
    public int MatchLimit
    {
        get => (int)Math.Min(NativeMethods.QueryCursorMatchLimit(Handle), int.MaxValue);
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            NativeMethods.QueryCursorSetMatchLimit(Handle, (uint)value);
        }
    }

    /// <summary>True when the last run dropped matches because of <see cref="MatchLimit"/>.</summary>
    public bool DidExceedMatchLimit => NativeMethods.QueryCursorDidExceedMatchLimit(Handle);

    private nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
            return _handle.DangerousGetHandle();
        }
    }

    /// <summary>
    /// The matches of <paramref name="query"/> under <paramref name="node"/>, in order, that
    /// pass the query's text predicates. <paramref name="source"/> is the buffer that was parsed.
    /// Starting another run on this cursor ends the previous enumeration.
    /// </summary>
    public IEnumerable<QueryMatch> Matches(Query query, Node node, ReadOnlyMemory<byte> source)
    {
        ArgumentNullException.ThrowIfNull(query);
        var tree = node.Tree;
        NativeMethods.QueryCursorSetByteRange(Handle, 0, uint.MaxValue);
        NativeMethods.QueryCursorExec(Handle, query.Handle, node.Raw);
        return Enumerate(query, tree, source);
    }

    /// <inheritdoc />
    public void Dispose() => _handle.Dispose();

    private IEnumerable<QueryMatch> Enumerate(Query query, Tree tree, ReadOnlyMemory<byte> source)
    {
        while (Next(query, tree, source.Span) is { } match)
        {
            yield return match;
        }
    }

    private unsafe QueryMatch? Next(Query query, Tree tree, ReadOnlySpan<byte> source)
    {
        while (true)
        {
            tree.ThrowIfDisposed();
            TSQueryMatch raw;
            if (!NativeMethods.QueryCursorNextMatch(Handle, &raw))
            {
                return null;
            }
            var captures = new QueryCapture[raw.CaptureCount];
            for (var i = 0; i < captures.Length; i++)
            {
                captures[i] = new QueryCapture(
                    new Node(raw.Captures[i].Node, tree),
                    (int)raw.Captures[i].Index
                );
            }
            if (query.SatisfiesTextPredicates(raw.PatternIndex, captures, source))
            {
                return new QueryMatch((int)raw.Id, raw.PatternIndex, captures);
            }
        }
    }
}
