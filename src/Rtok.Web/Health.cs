namespace Rtok.Web;

/// <summary>The <c>/health</c> body; it never waits on a snapshot build.</summary>
/// <param name="Ok">Always true while the server answers.</param>
/// <param name="Host">The bind host.</param>
/// <param name="Port">The bound port.</param>
internal sealed record Health(bool Ok, string Host, int Port);
