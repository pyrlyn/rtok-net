namespace Rtok.Mcp.Install;

/// <summary>
/// A host config file's syntax. <see cref="Json"/> is strict and rewritten as pretty JSON;
/// <see cref="Jsonc"/> and <see cref="Toml"/> keep the file's own formatting and comments.
/// </summary>
public enum McpFormat
{
    /// <summary>Strict JSON: comments are refused, never rewritten away.</summary>
    Json,

    /// <summary>JSON with comments and trailing commas, edited in place.</summary>
    Jsonc,

    /// <summary>TOML, edited in place; rtok's entry is a <c>[&lt;key&gt;.rtok]</c> table.</summary>
    Toml,
}
