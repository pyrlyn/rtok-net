namespace Rtok.Config;

/// <summary>tag each request's lane in the ledger (calls.kind); bytes stay identical</summary>
/// <remarks>Read from [proxy.lanes].</remarks>
public sealed class LanesSection
{
    /// <summary>false = every request an untagged api_request; x-rtok-lane and /lane/&lt;name&gt;/ forwarded as sent</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The [proxy.lanes.bulk] table.</summary>
    public LanePolicy Bulk { get; set; } = new();

    /// <summary>The [proxy.lanes.bulk] table.</summary>
    public LanePolicy Embeddings { get; set; } = new();

    /// <summary>The [proxy.lanes.bulk] table.</summary>
    public LanePolicy Meta { get; set; } = new();

    /// <summary>The [proxy.lanes.bulk] table.</summary>
    public LanePolicy Internal { get; set; } = new();
}
