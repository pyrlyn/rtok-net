namespace Rtok.Web.Contracts;

/// <summary>Start or stop the live calls frames; the page subscribes only while the panel is
/// visible.</summary>
/// <param name="Subscribe">True to start, false to stop.</param>
public sealed record CallsRequest(bool Subscribe);
