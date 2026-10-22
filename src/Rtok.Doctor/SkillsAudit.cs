using System.Text.Json.Serialization;

namespace Rtok.Doctor;

/// <summary>The skills hosts list on every request.</summary>
/// <param name="Rows">One row per listing, largest body first.</param>
/// <param name="DescBytes">The characters of all descriptions together.</param>
/// <param name="Duplicates">Skills one host lists more than once.</param>
public sealed record SkillsAudit(
    IReadOnlyList<SkillRow> Rows,
    int DescBytes,
    IReadOnlyList<SkillDuplicate>? Duplicates
);
