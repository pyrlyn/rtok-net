namespace Rtok.Proxy;

/// <summary>One stored answer, replayed as it came.</summary>
/// <param name="Status">The upstream status, always 2xx.</param>
/// <param name="ContentType">The upstream <c>Content-Type</c>.</param>
/// <param name="Body">The whole body.</param>
internal sealed record CachedAnswer(int Status, string? ContentType, byte[] Body);
