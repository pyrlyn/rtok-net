namespace Rtok.Plugins.Docs;

/// <summary>Where <c>rtok docs fetch</c> downloads from, and its limits. Tests point it at a fake.</summary>
public sealed record DocsUpstream
{
    /// <summary>docs.rs; a crate's rustdoc JSON is at <c>crate/&lt;name&gt;/&lt;version&gt;/json.zst</c>.</summary>
    public Uri DocsRs { get; init; } = new("https://docs.rs/");

    /// <summary>NuGet's flat container; a package is at <c>&lt;id&gt;/&lt;version&gt;/&lt;id&gt;.&lt;version&gt;.nupkg</c>, lower case.</summary>
    public Uri NuGet { get; init; } = new("https://api.nuget.org/v3-flatcontainer/");

    /// <summary>How long one download may take (rtok: 30 s).</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>A download larger than this is refused.</summary>
    public long MaxDownloadBytes { get; init; } = 64 << 20;

    /// <summary>Unpacked rustdoc JSON larger than this is refused.</summary>
    public long MaxUnpackedBytes { get; init; } = 512 << 20;

    /// <summary>The URL <paramref name="package"/>'s docs are fetched from.</summary>
    public Uri For(PinnedPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (package.Ecosystem == Ecosystem.Cargo)
        {
            return new Uri(DocsRs, $"crate/{package.Name}/{package.Version}/json.zst");
        }
        var (id, version) = (package.Name.ToLowerInvariant(), package.Version.ToLowerInvariant());
        return new Uri(NuGet, $"{id}/{version}/{id}.{version}.nupkg");
    }
}
