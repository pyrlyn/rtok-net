using System.Text;
using Pyrlyn.TreeSitter.Native;

namespace Pyrlyn.TreeSitter;

/// <summary>A node of a <see cref="Tree"/>. Byte offsets and points refer to the UTF-8 source.</summary>
public readonly struct Node : IEquatable<Node>
{
    private readonly Tree? _tree;

    internal Node(TSNode raw, Tree tree)
    {
        Raw = raw;
        _tree = tree;
    }

    internal TSNode Raw { get; }

    /// <summary>The tree the node belongs to.</summary>
    public Tree Tree => _tree ?? throw new InvalidOperationException("default(Node) has no tree.");

    /// <summary>True for a missing node: a child or sibling that does not exist.</summary>
    public bool IsNull => _tree is null || NativeMethods.NodeIsNull(Live);

    /// <summary>The node type, e.g. <c>function_item</c>, or the literal text of an anonymous node.</summary>
    public string Kind => Tree.Language.KindOf(Live);

    /// <summary>First byte of the node.</summary>
    public int StartByte => (int)NativeMethods.NodeStartByte(Live);

    /// <summary>One past the last byte of the node.</summary>
    public int EndByte => (int)NativeMethods.NodeEndByte(Live);

    /// <summary>Where the node starts.</summary>
    public Point StartPoint => ToPoint(NativeMethods.NodeStartPoint(Live));

    /// <summary>Where the node ends (exclusive).</summary>
    public Point EndPoint => ToPoint(NativeMethods.NodeEndPoint(Live));

    /// <summary>True for a named node (a grammar rule), false for anonymous tokens like <c>"("</c>.</summary>
    public bool IsNamed => NativeMethods.NodeIsNamed(Live);

    /// <summary>True when the parser inserted the node to recover from an error.</summary>
    public bool IsMissing => NativeMethods.NodeIsMissing(Live);

    /// <summary>True when the node is an ERROR node.</summary>
    public bool IsError => NativeMethods.NodeIsError(Live);

    /// <summary>True when the node or a descendant is an error or missing node.</summary>
    public bool HasError => NativeMethods.NodeHasError(Live);

    /// <summary>The parent, or a null node at the root.</summary>
    public Node Parent => Wrap(NativeMethods.NodeParent(Live));

    /// <summary>The next sibling, or a null node.</summary>
    public Node NextSibling => Wrap(NativeMethods.NodeNextSibling(Live));

    /// <summary>The previous sibling, or a null node.</summary>
    public Node PrevSibling => Wrap(NativeMethods.NodePrevSibling(Live));

    /// <summary>Number of children, named and anonymous.</summary>
    public int ChildCount => (int)NativeMethods.NodeChildCount(Live);

    /// <summary>Number of named children.</summary>
    public int NamedChildCount => (int)NativeMethods.NodeNamedChildCount(Live);

    /// <summary>The children in order, read with a tree cursor.</summary>
    public IEnumerable<Node> Children
    {
        get
        {
            var tree = Tree;
            var cursor = NativeMethods.TreeCursorNew(Live);
            try
            {
                if (!NativeMethods.TreeCursorGotoFirstChild(ref cursor))
                {
                    yield break;
                }
                do
                {
                    tree.ThrowIfDisposed();
                    yield return new Node(NativeMethods.TreeCursorCurrentNode(ref cursor), tree);
                } while (NativeMethods.TreeCursorGotoNextSibling(ref cursor));
            }
            finally
            {
                NativeMethods.TreeCursorDelete(ref cursor);
            }
        }
    }

    /// <summary>The node and all its descendants in document order (pre-order).</summary>
    public IEnumerable<Node> Descendants
    {
        get
        {
            var tree = Tree;
            var cursor = NativeMethods.TreeCursorNew(Live);
            try
            {
                var depth = 0;
                while (true)
                {
                    tree.ThrowIfDisposed();
                    yield return new Node(NativeMethods.TreeCursorCurrentNode(ref cursor), tree);
                    if (NativeMethods.TreeCursorGotoFirstChild(ref cursor))
                    {
                        depth++;
                        continue;
                    }
                    // The cursor never leaves the node it started at, so at depth 0 there is
                    // no sibling to move to and the walk is over.
                    while (depth == 0 || !NativeMethods.TreeCursorGotoNextSibling(ref cursor))
                    {
                        if (depth == 0 || !NativeMethods.TreeCursorGotoParent(ref cursor))
                        {
                            yield break;
                        }
                        depth--;
                    }
                }
            }
            finally
            {
                NativeMethods.TreeCursorDelete(ref cursor);
            }
        }
    }

    private TSNode Live
    {
        get
        {
            Tree.ThrowIfDisposed();
            return Raw;
        }
    }

    /// <summary>The child at <paramref name="index"/>, or a null node.</summary>
    public Node Child(int index) => Wrap(NativeMethods.NodeChild(Live, (uint)index));

    /// <summary>The named child at <paramref name="index"/>, or a null node.</summary>
    public Node NamedChild(int index) => Wrap(NativeMethods.NodeNamedChild(Live, (uint)index));

    /// <summary>The child under field <paramref name="field"/>, or a null node.</summary>
    public unsafe Node ChildByFieldName(string field)
    {
        ArgumentNullException.ThrowIfNull(field);
        var name = Encoding.UTF8.GetBytes(field);
        fixed (byte* p = name)
        {
            return Wrap(NativeMethods.NodeChildByFieldName(Live, p, (uint)name.Length));
        }
    }

    /// <summary>The node's bytes within <paramref name="source"/>, the buffer that was parsed.</summary>
    public ReadOnlySpan<byte> Text(ReadOnlySpan<byte> source)
    {
        var start = Math.Min(StartByte, source.Length);
        var end = Math.Clamp(EndByte, start, source.Length);
        return source[start..end];
    }

    /// <inheritdoc />
    public bool Equals(Node other) =>
        ReferenceEquals(_tree, other._tree) && Raw.Id == other.Raw.Id && Raw.Tree == other.Raw.Tree;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Node other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Raw.Id, Raw.Tree);

    /// <inheritdoc />
    public override string ToString() =>
        IsNull
            ? "(null)"
            : $"({Kind} {StartPoint.Row}:{StartPoint.Column}-{EndPoint.Row}:{EndPoint.Column})";

    /// <summary>Same node.</summary>
    public static bool operator ==(Node left, Node right) => left.Equals(right);

    /// <summary>Different nodes.</summary>
    public static bool operator !=(Node left, Node right) => !left.Equals(right);

    private Node Wrap(TSNode raw) => new(raw, Tree);

    private static Point ToPoint(TSPoint p) => new((int)p.Row, (int)p.Column);
}
