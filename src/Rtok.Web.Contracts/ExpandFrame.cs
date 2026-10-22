namespace Rtok.Web.Contracts;

/// <summary>The archived payload a client asked for, byte for byte.</summary>
/// <param name="Id">The archive id.</param>
/// <param name="Text">The payload.</param>
public sealed record ExpandFrame(string Id, string Text) : ServerFrame;
