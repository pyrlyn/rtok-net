namespace Rtok.Proxy;

/// <summary>The kind of traffic a request is (rtok T385): the agent's own turns, or one of the
/// lanes a path or a marker names. Only the agent lane follows every global switch; the others
/// get what <c>[proxy.lanes.&lt;lane&gt;]</c> opens.</summary>
public enum Lane
{
    /// <summary>The agent's turns: never capped, queued, routed or turned into a batch.</summary>
    Agent,

    /// <summary>Scripts and sweeps that can wait.</summary>
    Bulk,

    /// <summary>The providers' own Batch APIs; forwarded as sent.</summary>
    Batch,

    /// <summary>The Files APIs; forwarded as sent.</summary>
    Files,

    /// <summary>Embedding endpoints.</summary>
    Embeddings,

    /// <summary>Model lists and token counts.</summary>
    Meta,

    /// <summary>rtok's own calls (the summary compressor).</summary>
    Internal,
}
