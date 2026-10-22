using System.Text.Json.Serialization;

namespace Rtok.Doctor;

/// <summary>One listed skill.</summary>
/// <param name="Name">Its directory name.</param>
/// <param name="Source"><c>user</c>, <c>project</c>, <c>plugin:&lt;id&gt;</c>.</param>
/// <param name="DescChars">The description's length.</param>
/// <param name="BodyBytes">The body after the front matter, in bytes.</param>
/// <param name="WarnDesc">The description is over 120 characters.</param>
/// <param name="WarnBody">The body is over 8 KiB.</param>
public sealed record SkillRow(
    string Name,
    string Source,
    int DescChars,
    int BodyBytes,
    bool WarnDesc,
    bool WarnBody
);
