using System.Text.Json.Serialization;

namespace Rtok.Doctor;

/// <summary>A skill one host lists from several roots.</summary>
/// <param name="Name">The skill.</param>
/// <param name="Copies">Each listing's source and directory.</param>
/// <param name="Identical">All bodies are equal.</param>
/// <param name="ExtraTokens">The tokens the extra listings cost per request.</param>
public sealed record SkillDuplicate(
    string Name,
    IReadOnlyList<SkillCopy> Copies,
    bool Identical,
    int ExtraTokens
);
