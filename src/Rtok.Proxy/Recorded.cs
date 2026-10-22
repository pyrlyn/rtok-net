namespace Rtok.Proxy;

/// <summary>One recorded request: its session, the model it named, its <c>calls</c> row.</summary>
internal sealed record Recorded(string Session, string? Model, long CallId);
