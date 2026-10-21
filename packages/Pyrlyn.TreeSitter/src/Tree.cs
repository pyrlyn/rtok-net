using Pyrlyn.TreeSitter.Native;

namespace Pyrlyn.TreeSitter;

/// <summary>
/// A syntax tree. Its nodes stay valid until the tree is disposed; reading a node of a
/// disposed tree throws <see cref="ObjectDisposedException"/>.
/// </summary>
public sealed class Tree : IDisposable
{
    private readonly TreeHandle _handle;

    internal Tree(nint handle, Language language)
    {
        _handle = new TreeHandle(handle);
        Language = language;
    }

    /// <summary>The grammar that produced the tree.</summary>
    public Language Language { get; }

    /// <summary>The root node.</summary>
    public Node Root => new(NativeMethods.TreeRootNode(Handle), this);

    internal nint Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle.DangerousGetHandle();
        }
    }

    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_handle.IsClosed, this);

    /// <inheritdoc />
    public void Dispose() => _handle.Dispose();
}
