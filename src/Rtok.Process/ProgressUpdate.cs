namespace Rtok.Process;

/// <summary>
/// One measurable step. A null <see cref="Total"/> carries only a message: the indicator stays a
/// spinner.
/// </summary>
/// <param name="Position">How far the work is.</param>
/// <param name="Total">Where it ends, or null when that is unknown.</param>
/// <param name="Unit">What the two count.</param>
/// <param name="Message">What runs now, shown after the label; null keeps the last one.</param>
public sealed record ProgressUpdate(long Position, long? Total, ProgressUnit Unit, string? Message);
