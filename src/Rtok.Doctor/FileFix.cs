namespace Rtok.Doctor;

/// <summary>One file's planned edit.</summary>
/// <param name="Source">The file.</param>
/// <param name="Entries">The selected entries in it.</param>
/// <param name="Before">Its text at the check.</param>
/// <param name="After">The text to write.</param>
/// <param name="Diff">The unified diff.</param>
/// <param name="Error">Why it is skipped; null when it can be written.</param>
internal sealed record FileFix(
    string Source,
    IReadOnlyList<Problem> Entries,
    string Before,
    string After,
    string Diff,
    string? Error
);
