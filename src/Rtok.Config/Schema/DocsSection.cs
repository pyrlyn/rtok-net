namespace Rtok.Config;

/// <summary>local Rust crate docs</summary>
/// <remarks>Read from [plugins.docs].</remarks>
public sealed class DocsSection
{
    /// <summary>local rustdoc from Cargo.lock + cached docs.rs JSON</summary>
    public bool Enabled { get; set; }

    /// <summary>docs_query hit ceiling</summary>
    public uint QueryLimit { get; set; } = 5;

    /// <summary>chars per FTS snippet</summary>
    public uint SnippetChars { get; set; } = 400;

    /// <summary>whole-reply estimate cap</summary>
    public uint MaxTokens { get; set; } = 800;
}
