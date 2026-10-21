using System.Text.Json.Serialization;

namespace Rtok.Agents.Hosts;

/// <summary>Where a module stands on one app.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ModuleState>))]
public enum ModuleState
{
    /// <summary>rtok's entry is there.</summary>
    [JsonStringEnumMemberName("installed")]
    Installed,

    /// <summary>The host can take it; rtok's entry is not there.</summary>
    [JsonStringEnumMemberName("not_installed")]
    NotInstalled,

    /// <summary>The host cannot take it.</summary>
    [JsonStringEnumMemberName("not_supported")]
    NotSupported,
}
