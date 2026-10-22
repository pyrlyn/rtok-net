using Rtok.Archive;

namespace Rtok.Web;

/// <summary>How <c>rtok web</c> binds and what it serves.</summary>
/// <param name="Host">The bind address: an IP literal or <c>localhost</c>.</param>
/// <param name="Port">The port; 0 lets the system pick one.</param>
/// <param name="Files">The client files.</param>
public sealed record WebOptions(string Host, int Port, ClientFiles Files)
{
    /// <summary>Serve <c>/ws</c> without the per-run token; refused on a non-loopback host.</summary>
    public bool NoToken { get; init; }

    /// <summary>Answers expand over <c>/ws</c>, the handler <c>rtok expand</c> uses; without
    /// one, an expand is refused.</summary>
    public Expander? Expander { get; init; }

    /// <summary>How often the snapshot is rebuilt.</summary>
    public TimeSpan Tick { get; init; } = TimeSpan.FromSeconds(2);
}
