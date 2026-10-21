using System.Text.Json.Nodes;

namespace Rtok.Mcp.Install;

/// <summary>
/// Reads, sets and removes one entry in a host config's servers table, whatever the file's
/// syntax. Schema-less: only <c>&lt;key path&gt;.&lt;name&gt;</c> is touched, every other byte of
/// a JSONC or TOML file stays as it was. Text in, text out; <see cref="McpInstaller"/> does the I/O.
/// </summary>
public static class McpConfig
{
    /// <summary>The servers table at the spec's key path; null when the file, the table or an object on the way is absent.</summary>
    /// <param name="spec">The surface.</param>
    /// <param name="text">The file's text, or null for no file.</param>
    /// <exception cref="InvalidDataException">The text does not parse in the spec's format.</exception>
    public static JsonObject? Servers(McpSpec spec, string? text)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return text is null
            ? null
            : spec.Format switch
            {
                McpFormat.Json => JsonServers.Servers(spec, text),
                McpFormat.Jsonc => JsoncServers.Servers(spec, text),
                _ => TomlServers.Servers(spec, text),
            };
    }

    /// <summary>The entry under the server's name; null when absent.</summary>
    /// <exception cref="InvalidDataException">The text does not parse in the spec's format.</exception>
    public static JsonNode? Entry(McpSpec spec, string? text) =>
        Servers(spec, text)?[spec.Server.Name]?.DeepClone();

    /// <summary>The text with <paramref name="entry"/> under the server's name, tables on the way created.</summary>
    /// <exception cref="InvalidDataException">The text does not parse, or the root or a table on the way is not one.</exception>
    public static string With(McpSpec spec, string? text, JsonObject entry)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(entry);
        return spec.Format switch
        {
            McpFormat.Json => JsonServers.With(spec, text ?? "", entry),
            McpFormat.Jsonc => JsoncServers.With(spec, text ?? "", entry),
            _ => TomlServers.With(spec, text ?? "", entry),
        };
    }

    /// <summary>The text without the entry; tables rtok's entry alone kept alive go with it.</summary>
    /// <exception cref="InvalidDataException">The text does not parse, or the entry cannot be cut out of it.</exception>
    public static string Without(McpSpec spec, string text)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(text);
        return spec.Format switch
        {
            McpFormat.Json => JsonServers.Without(spec, text),
            McpFormat.Jsonc => JsoncServers.Without(spec, text),
            _ => TomlServers.Without(spec, text),
        };
    }
}
