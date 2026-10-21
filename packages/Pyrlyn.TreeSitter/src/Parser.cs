using System.Text;
using Pyrlyn.TreeSitter.Native;

namespace Pyrlyn.TreeSitter;

/// <summary>Parses UTF-8 source into a <see cref="Tree"/>. Not thread-safe: one parser per thread.</summary>
public sealed class Parser : IDisposable
{
    private readonly ParserHandle _handle = new();
    private Language? _language;

    /// <summary>A parser with no language; set <see cref="Language"/> before parsing.</summary>
    public Parser() { }

    /// <summary>A parser for <paramref name="language"/>.</summary>
    public Parser(Language language) => Language = language;

    /// <summary>The grammar the next parse uses.</summary>
    /// <exception cref="InvalidOperationException">The grammar's ABI is not supported by the runtime.</exception>
    public Language? Language
    {
        get => _language;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!NativeMethods.ParserSetLanguage(Handle, value.Handle))
            {
                throw new InvalidOperationException(
                    $"tree-sitter cannot load grammar {value.Name} (ABI {value.AbiVersion})."
                );
            }
            _language = value;
        }
    }

    private nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
            return _handle.DangerousGetHandle();
        }
    }

    /// <summary>Parses UTF-8 bytes. Returns null when no language is set or the parse was cut off.</summary>
    public unsafe Tree? Parse(ReadOnlySpan<byte> utf8)
    {
        if (_language is null)
        {
            return null;
        }
        nint tree;
        fixed (byte* text = utf8)
        {
            tree = NativeMethods.ParserParseString(Handle, 0, text, (uint)utf8.Length);
        }
        return tree == 0 ? null : new Tree(tree, _language);
    }

    /// <summary>Parses a string, encoded as UTF-8; byte offsets in the tree refer to that encoding.</summary>
    public Tree? Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Parse(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>Forgets any half-finished parse, so the next one starts fresh.</summary>
    public void Reset() => NativeMethods.ParserReset(Handle);

    /// <inheritdoc />
    public void Dispose() => _handle.Dispose();
}
