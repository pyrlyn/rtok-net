namespace Rtok.Config;

/// <summary>one rule per lane; it acts only with from, to and gate all set</summary>
/// <remarks>Read from [proxy.routing.bulk], [proxy.routing.internal].</remarks>
public sealed class RouteRule
{
    /// <summary>request models it applies to: an exact id or a prefix ending in * (empty = off)</summary>
    public IList<string> From { get; set; } = [];

    /// <summary>the model that serves a matching call (empty = off)</summary>
    public string To { get; set; } = "";

    /// <summary>provider slug recorded for a routed call (empty = the wire's own); pair with [proxy.lanes.&lt;lane&gt;] upstream</summary>
    public string Provider { get; set; } = "";

    /// <summary>where the quality bench for this swap is kept (a path, URL or run id); empty = off</summary>
    public string Gate { get; set; } = "";
}
