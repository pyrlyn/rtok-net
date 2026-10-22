using System.Text.Json.Serialization;

namespace Rtok.Doctor;

/// <summary>A line two or more instruction sources carry.</summary>
/// <param name="Sentence">Its first 60 characters.</param>
/// <param name="Names">The sources, sorted.</param>
public sealed record InstructionDuplicate(string Sentence, IReadOnlyList<string> Names);
