namespace Rtok.Web.Contracts;

/// <summary>A write request succeeded; the next snapshot shows its effect.</summary>
/// <param name="Text">What was written, one line.</param>
public sealed record DoneFrame(string Text) : ServerFrame;
