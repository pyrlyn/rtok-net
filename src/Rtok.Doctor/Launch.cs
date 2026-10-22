namespace Rtok.Doctor;

/// <summary>How an MCP entry starts: the key two copies share, the pinned version, the argv shown.</summary>
/// <param name="Key">The launch with paths resolved and the env folded in; never printed.</param>
/// <param name="Version">The <c>pkg@version</c> pin of a package runner; null when unpinned.</param>
/// <param name="Shown">The URL or argv as written, without env values.</param>
internal readonly record struct Launch(string Key, string? Version, string Shown);
