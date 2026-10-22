using System.Text.Json.Serialization;

namespace Rtok.Doctor;

/// <summary>The instruction files every session loads, and lines several of them repeat.</summary>
/// <param name="Rows">One row per file.</param>
/// <param name="Duplicates">Each repeated line (its first 60 characters) and the files that carry it.</param>
public sealed record InstructionAudit(
    IReadOnlyList<InstructionRow> Rows,
    IReadOnlyList<InstructionDuplicate> Duplicates
);
