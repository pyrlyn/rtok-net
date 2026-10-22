using Rtok.Plugins.Docs;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any text as a <c>Cargo.lock</c>, a <c>packages.lock.json</c> or an <c>llms.txt</c>, the first
/// byte picking which; text before a NUL is the package asked for (else <c>serde</c>). No reader
/// throws, a lockfile pins the name asked for at a version safe as a path, and every
/// <c>llms.txt</c> item and link is non-empty.
/// </summary>
internal static class Lockfiles
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        var (pick, text) = FuzzInput.Picked(data);
        var parts = text.Split('\0', 2);
        var (name, body) = parts.Length == 2 ? (parts[0], parts[1]) : ("serde", text);
        switch (pick % 3)
        {
            case 0:
                Pinned(CargoLock.Resolve(body, name), name, StringComparison.Ordinal);
                break;
            case 1:
                Pinned(NuGetLock.Resolve(body, name), name, StringComparison.OrdinalIgnoreCase);
                break;
            default:
                foreach (var item in LlmsText.Split(body))
                {
                    FuzzInput.Check(
                        item.Path.Length > 0
                            && item.Docs.Length > 0
                            && string.Equals(item.Docs.Trim(), item.Docs, StringComparison.Ordinal),
                        $"empty or untrimmed llms item {item}"
                    );
                }
                foreach (var link in LlmsText.LocalLinks(body))
                {
                    FuzzInput.Check(
                        link.Length > 0 && !link.Contains(':', StringComparison.Ordinal),
                        $"non-local link {link}"
                    );
                }
                break;
        }
    }

    private static void Pinned(PinnedPackage? pinned, string name, StringComparison comparison)
    {
        if (pinned is null)
        {
            return;
        }
        FuzzInput.Check(
            string.Equals(pinned.Name, name, comparison)
                && Plugins.Docs.Lockfiles.Safe(pinned.Name)
                && Plugins.Docs.Lockfiles.Safe(pinned.Version),
            $"{pinned} for {name}"
        );
    }
}
