using Pyrlyn.Config.Layered;
using Tomlyn.Model;

namespace Rtok.Plugins.Docs;

/// <summary><c>Cargo.lock</c>: the <c>[[package]]</c> tables, first exact name wins (rtok's rule).</summary>
public static class CargoLock
{
    /// <summary>The package <paramref name="name"/> pins, or null when there is none or the text is not a lockfile.</summary>
    public static PinnedPackage? Resolve(string text, string name)
    {
        ArgumentNullException.ThrowIfNull(text);
        TomlTable root;
        try
        {
            root = TomlText.Parse(text);
        }
        catch (ConfigFileException)
        {
            return null;
        }
        if (!root.TryGetValue("package", out var value) || value is not TomlTableArray packages)
        {
            return null;
        }
        foreach (var package in packages)
        {
            if (
                package.TryGetValue("name", out var n)
                && n is string found
                && string.Equals(found, name, StringComparison.Ordinal)
            )
            {
                return
                    package.TryGetValue("version", out var v)
                    && v is string version
                    && Lockfiles.Safe(found)
                    && Lockfiles.Safe(version)
                    ? new PinnedPackage(found, version, Ecosystem.Cargo)
                    : null;
            }
        }
        return null;
    }
}
