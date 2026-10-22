using System.Text.Json;

namespace Rtok.Store;

/// <summary>Who a <see cref="StoreRuntime"/> writes for.</summary>
/// <param name="Session">The host session every row is attributed to.</param>
public sealed record StoreRuntimeOptions(string Session)
{
    /// <summary>The surface's working directory, when it has one.</summary>
    public string? Cwd { get; init; }

    /// <summary>The project the session works in, for its <c>sessions</c> row.</summary>
    public string? Project { get; init; }

    /// <summary>The seeded <c>hosts</c> id of the agent host.</summary>
    public long? HostId { get; init; }

    /// <summary>The delivery key (<c>PreToolUse:&lt;tool_use_id&gt;</c>): a measurement this
    /// delivery already stored is dropped, so a call delivered twice counts once.</summary>
    public string? Once { get; init; }

    /// <summary>The configuration by dotted path, as <c>IHost.ConfigJson</c> answers it.</summary>
    public Func<string, JsonElement?> Config { get; init; } = _ => null;
}
