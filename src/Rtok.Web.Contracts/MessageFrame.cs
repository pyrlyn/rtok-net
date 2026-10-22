namespace Rtok.Web.Contracts;

/// <summary>A one-line notice for the operator: a refused or failed request.</summary>
/// <param name="Text">The notice.</param>
public sealed record MessageFrame(string Text) : ServerFrame;
