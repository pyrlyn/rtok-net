namespace Rtok.Plugin.Sdk;

/// <summary>An injected skill body in a user message.</summary>
/// <param name="Id">The preceding <c>Skill</c> tool_use id.</param>
/// <param name="Name">Last path component of the skill directory.</param>
/// <param name="Content">The skill body text block; rewrite it in place.</param>
/// <param name="Turn">User turns since this block was injected.</param>
public sealed record SkillRef(string Id, string Name, JsonSlot Content, int Turn);
