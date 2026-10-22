namespace Rtok.Proxy;

/// <summary>What a routing rule changed, for the ledger.</summary>
/// <param name="Body">The request with the new model.</param>
/// <param name="From">The model the client asked for.</param>
/// <param name="To">The model it was sent to.</param>
/// <param name="Provider">The provider slug the call is recorded under.</param>
internal sealed record Routed(byte[] Body, string From, string To, string Provider);
