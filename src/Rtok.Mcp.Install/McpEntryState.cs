using System.Text.Json.Serialization;

namespace Rtok.Mcp.Install;

/// <summary>What a surface's config holds under rtok's name, compared with what rtok writes.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<McpEntryState>))]
public enum McpEntryState
{
    /// <summary>An entry that runs <c>rtok mcp</c> for this host.</summary>
    [JsonStringEnumMemberName("present")]
    Present,

    /// <summary>No entry, or a file rtok cannot read.</summary>
    [JsonStringEnumMemberName("missing")]
    Missing,

    /// <summary>An entry under rtok's name that runs something else.</summary>
    [JsonStringEnumMemberName("stale")]
    Stale,
}
