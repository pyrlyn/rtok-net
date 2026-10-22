namespace Rtok.Doctor;

/// <summary>What applying a <see cref="FixRequest"/> would write, and the token that confirms it.</summary>
/// <param name="Items">Every removable entry, selected or not.</param>
/// <param name="Diff">The diff per edited file, or why a file would be skipped.</param>
/// <param name="Refused">The entries that are not removed and why.</param>
/// <param name="Token">The hex SHA-256 of the selection and the diff; applying needs it back unchanged.</param>
public sealed record FixPlan(
    IReadOnlyList<FixItem> Items,
    string Diff,
    IReadOnlyList<string> Refused,
    string Token
);
