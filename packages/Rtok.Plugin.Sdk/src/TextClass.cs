namespace Rtok.Plugin.Sdk;

/// <summary>What kind of text is being measured. The host keeps a chars-per-token rate per
/// class, so an estimate stays within about 15 % without a tokenizer or a network call.</summary>
public enum TextClass
{
    /// <summary>Source code, shell output, diffs.</summary>
    Code,

    /// <summary>Natural-language prose, markdown.</summary>
    Prose,

    /// <summary>JSON and other structured data: punctuation-heavy, fewer chars per
    /// token.</summary>
    Json,

    /// <summary>CJK scripts: roughly one token per character.</summary>
    Cjk,
}
