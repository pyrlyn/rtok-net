using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Rtok.Plugin.Sdk;

/// <summary>
/// What a plugin is handed on every event: the host, and the context window it runs in. The
/// host builds one per dispatch.
/// </summary>
/// <param name="host">The host for this dispatch.</param>
/// <param name="agentId">The sub-agent whose window this dispatch belongs to, or
/// <see langword="null"/> for the parent window.</param>
public sealed class Ctx(ICapabilities host, string? agentId = null)
{
    /// <summary>The host for this dispatch.</summary>
    public ICapabilities Host { get; } = host;

    /// <summary>
    /// The sub-agent this dispatch belongs to, or <see langword="null"/> for the parent window.
    /// A window is <c>(session, agent)</c>: hooks fired in a sub-agent carry its id, and
    /// already-seen state must not cross windows.
    /// </summary>
    public string? AgentId { get; } = agentId;

    /// <summary>
    /// This plugin's own <c>[plugins.&lt;id&gt;]</c> section. A missing or unparsable section
    /// yields <c>new T()</c>: a plugin that cannot read its settings still runs.
    /// </summary>
    public T PluginConfig<T>(string id, JsonTypeInfo<T> typeInfo)
        where T : new() => Config("plugins." + id, typeInfo);

    /// <summary>A host-wide section by dotted path, or <c>new T()</c> when the host has none or
    /// it does not parse. Reach for <see cref="PluginConfig{T}"/> first.</summary>
    public T Config<T>(string path, JsonTypeInfo<T> typeInfo)
        where T : new()
    {
        if (Host.ConfigJson(path) is not { } section)
        {
            return new T();
        }

        try
        {
            return section.Deserialize(typeInfo) ?? new T();
        }
        catch (JsonException)
        {
            return new T();
        }
    }
}
