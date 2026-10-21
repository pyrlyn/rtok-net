namespace Rtok.Plugin.Sdk;

/// <summary>A session starting or resuming.</summary>
/// <param name="Source"><c>startup</c>, <c>resume</c>, <c>clear</c> or <c>compact</c>.</param>
public sealed record SessionStart(string Source);
