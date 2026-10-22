using System.Text.Json;

namespace Rtok.Plugins.Docs;

/// <summary>
/// <c>packages.lock.json</c>: <c>dependencies.&lt;framework&gt;.&lt;id&gt;.resolved</c>. NuGet ids
/// match case-insensitively; the first framework, in file order, that resolves the id wins.
/// </summary>
public static class NuGetLock
{
    private static readonly JsonDocumentOptions Options = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>The package <paramref name="name"/> pins, or null when there is none or the text is not a lockfile.</summary>
    public static PinnedPackage? Resolve(string text, string name)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            using var doc = JsonDocument.Parse(text, Options);
            if (
                doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("dependencies", out var frameworks)
                || frameworks.ValueKind != JsonValueKind.Object
            )
            {
                return null;
            }
            foreach (var framework in frameworks.EnumerateObject())
            {
                if (framework.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                foreach (var package in framework.Value.EnumerateObject())
                {
                    if (
                        string.Equals(package.Name, name, StringComparison.OrdinalIgnoreCase)
                        && package.Value.ValueKind == JsonValueKind.Object
                        && package.Value.TryGetProperty("resolved", out var resolved)
                        && resolved.ValueKind == JsonValueKind.String
                        && resolved.GetString() is { } version
                        && Lockfiles.Safe(package.Name)
                        && Lockfiles.Safe(version)
                    )
                    {
                        return new PinnedPackage(package.Name, version, Ecosystem.NuGet);
                    }
                }
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
