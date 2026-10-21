using System.Runtime.InteropServices;
using Pyrlyn.TreeSitter.Native;

namespace Pyrlyn.TreeSitter;

/// <summary>
/// A grammar linked into the native library. Languages live for the whole process; get one from
/// the static properties or from <see cref="Grammar"/>.
/// </summary>
public sealed class Language
{
    private readonly string?[] _kinds;

    private Language(string name, nint handle)
    {
        Name = name;
        Handle = handle;
        _kinds = new string?[NativeMethods.LanguageSymbolCount(handle)];
    }

    /// <summary>The grammar's name, as its C entry point spells it (<c>tree_sitter_&lt;name&gt;</c>).</summary>
    public string Name { get; }

    /// <summary>The ABI version the grammar was generated for.</summary>
    public uint AbiVersion => NativeMethods.LanguageAbiVersion(Handle);

    internal nint Handle { get; }

    /// <summary>tree-sitter-c 0.24.2.</summary>
    public static Language C { get; } = new("c", NativeMethods.GrammarC());

    /// <summary>tree-sitter-c-sharp 0.23.5.</summary>
    public static Language CSharp { get; } = new("c_sharp", NativeMethods.GrammarCSharp());

    /// <summary>tree-sitter-dart 0.2.0.</summary>
    public static Language Dart { get; } = new("dart", NativeMethods.GrammarDart());

    /// <summary>tree-sitter-go 0.25.0.</summary>
    public static Language Go { get; } = new("go", NativeMethods.GrammarGo());

    /// <summary>tree-sitter-java 0.23.5.</summary>
    public static Language Java { get; } = new("java", NativeMethods.GrammarJava());

    /// <summary>tree-sitter-javascript 0.25.0 (JSX included).</summary>
    public static Language JavaScript { get; } =
        new("javascript", NativeMethods.GrammarJavaScript());

    /// <summary>tree-sitter-kotlin-ng 1.1.0.</summary>
    public static Language Kotlin { get; } = new("kotlin", NativeMethods.GrammarKotlin());

    /// <summary>tree-sitter-php 0.25.1, the grammar with embedded HTML.</summary>
    public static Language Php { get; } = new("php", NativeMethods.GrammarPhp());

    /// <summary>tree-sitter-python 0.25.0.</summary>
    public static Language Python { get; } = new("python", NativeMethods.GrammarPython());

    /// <summary>tree-sitter-ruby 0.23.1.</summary>
    public static Language Ruby { get; } = new("ruby", NativeMethods.GrammarRuby());

    /// <summary>tree-sitter-rust 0.24.2.</summary>
    public static Language Rust { get; } = new("rust", NativeMethods.GrammarRust());

    /// <summary>tree-sitter-swift 0.7.4.</summary>
    public static Language Swift { get; } = new("swift", NativeMethods.GrammarSwift());

    /// <summary>tree-sitter-typescript 0.23.2, the TSX dialect.</summary>
    public static Language Tsx { get; } = new("tsx", NativeMethods.GrammarTsx());

    /// <summary>tree-sitter-typescript 0.23.2.</summary>
    public static Language TypeScript { get; } =
        new("typescript", NativeMethods.GrammarTypeScript());

    /// <inheritdoc />
    public override string ToString() => Name;

    // ts_node_type is the name of the node's (aliased) symbol, so one string per symbol serves
    // every node. ERROR (65535) and other symbols outside the table are read each time.
    internal unsafe string KindOf(TSNode node)
    {
        var symbol = NativeMethods.NodeSymbol(node);
        if (symbol < _kinds.Length && _kinds[symbol] is { } cached)
        {
            return cached;
        }
        var kind = Marshal.PtrToStringUTF8((nint)NativeMethods.NodeType(node)) ?? "";
        if (symbol < _kinds.Length)
        {
            _kinds[symbol] = kind;
        }
        return kind;
    }
}
