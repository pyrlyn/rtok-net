namespace Rtok.Config;

/// <summary>model routing; only the internal and bulk lanes, never the agent lane</summary>
/// <remarks>Read from [proxy.routing].</remarks>
public sealed class RoutingPolicy
{
    /// <summary>off: every call keeps the model the client sent</summary>
    public bool Enabled { get; set; }

    /// <summary>The [proxy.routing.bulk] table.</summary>
    public RouteRule Bulk { get; set; } = new();

    /// <summary>The [proxy.routing.bulk] table.</summary>
    public RouteRule Internal { get; set; } = new();
}
