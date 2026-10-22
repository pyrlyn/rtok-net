using System.Text.Json.Serialization;

namespace Rtok.Doctor;

/// <summary>One listing of a duplicate skill.</summary>
/// <param name="Source">The root's source name.</param>
/// <param name="Dir">The skill's directory.</param>
public sealed record SkillCopy(string Source, string Dir);
