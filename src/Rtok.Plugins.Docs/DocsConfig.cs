namespace Rtok.Plugins.Docs;

/// <summary>The plugin's <c>[plugins.docs]</c> section.</summary>
public sealed class DocsConfig
{
    /// <summary>The most hits one <c>docs_query</c> considers.</summary>
    public int QueryLimit { get; set; } = 5;

    /// <summary>Characters of each hit's body shown as its snippet.</summary>
    public int SnippetChars { get; set; } = 400;

    /// <summary>The estimate a whole <c>docs_query</c> reply stays under.</summary>
    public int MaxTokens { get; set; } = 800;
}
