namespace Rtok.Plugins.Docs;

/// <summary>A dependency at the exact version a lockfile pins.</summary>
public sealed record PinnedPackage(string Name, string Version, Ecosystem Ecosystem)
{
    /// <summary>
    /// The name its docs are stored under: a crate by its own name (as rtok stored it), a NuGet
    /// id lower-cased behind <c>nuget:</c>, so the two never share a row and ids match any case.
    /// </summary>
    public string StoreName =>
        Ecosystem == Ecosystem.NuGet ? "nuget:" + Name.ToLowerInvariant() : Name;
}
