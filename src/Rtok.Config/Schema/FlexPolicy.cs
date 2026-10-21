using System.Text.Json.Serialization;

namespace Rtok.Config;

/// <summary>OpenAI Flex tier; which lanes get it is [proxy.lanes.&lt;lane&gt;] flex</summary>
/// <remarks>Read from [proxy.flex].</remarks>
public sealed class FlexPolicy
{
    /// <summary>overwrite a service_tier the client sent (off: a client value is never changed)</summary>
    public bool Force { get; set; }

    /// <summary>Flex has no capacity: none = the 429 goes to the client | backoff = retry on Flex | default = retry once on service_tier "auto" (the client's own tier, if force replaced one)</summary>
    [JsonPropertyName("on_429")]
    public string On429 { get; set; } = "none";

    /// <summary>backoff only: retries before giving up; at most 5</summary>
    public uint Retries { get; set; } = 3;

    /// <summary>backoff only: delay before the first retry, doubled each time, capped at 30 s; a 429's Retry-After (seconds) can lengthen it, up to that cap</summary>
    public ulong BackoffMs { get; set; } = 1000;
}
