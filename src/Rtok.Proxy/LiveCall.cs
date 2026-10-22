namespace Rtok.Proxy;

/// <summary>One request plain mode forwarded without recording: what <c>/live</c> shows.</summary>
/// <param name="Ts">Unix seconds when it finished.</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The request path.</param>
/// <param name="Provider">The wire's provider, when a wire owns the path.</param>
/// <param name="Model">The model the body names.</param>
/// <param name="Status">The status the client got.</param>
/// <param name="RequestBytes">Request body bytes.</param>
/// <param name="ResponseBytes">Response body bytes.</param>
/// <param name="Ms">Wall time.</param>
public sealed record LiveCall(
    long Ts,
    string Method,
    string Path,
    string? Provider,
    string? Model,
    int Status,
    long RequestBytes,
    long ResponseBytes,
    double Ms
);
