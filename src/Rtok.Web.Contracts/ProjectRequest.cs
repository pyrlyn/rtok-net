namespace Rtok.Web.Contracts;

/// <summary>A project registry write the graph page offers: <c>link</c> or <c>unlink</c>
/// two projects, as <c>rtok graph projects</c> does.</summary>
/// <param name="Action"><c>link</c> or <c>unlink</c>.</param>
/// <param name="From">The project that uses the other.</param>
/// <param name="To">The project used.</param>
/// <param name="Both">Both directions.</param>
public sealed record ProjectRequest(string Action, string From, string To, bool Both = false);
