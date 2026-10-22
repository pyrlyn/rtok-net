using Pyrlyn.Config.Layered;
using Tomlyn.Model;

namespace Rtok.Config;

/// <summary>The effective configuration: typed, as a tree, and leaf by leaf with sources.</summary>
public sealed class LoadedConfig
{
    private readonly Dictionary<string, ConfigEntry> _byKey;

    internal LoadedConfig(
        RtokConfig config,
        TomlTable values,
        IReadOnlyList<ConfigEntry> entries,
        IReadOnlyList<ConfigIssue> issues,
        IReadOnlyList<string> notes,
        ConfigLocations locations
    )
    {
        Config = config;
        Values = values;
        Entries = entries;
        Issues = issues;
        Notes = notes;
        Locations = locations;
        _byKey = entries.ToDictionary(e => e.Key, StringComparer.Ordinal);
    }

    /// <summary>The typed configuration. A value with an issue is left at its default.</summary>
    public RtokConfig Config { get; }

    /// <summary>The effective tree, paths expanded, legacy keys folded into their new keys.</summary>
    public TomlTable Values { get; }

    /// <summary>Every effective leaf with the layer that set it, sorted by key.</summary>
    public IReadOnlyList<ConfigEntry> Entries { get; }

    /// <summary>Values that were rejected: unknown keys, wrong types, out of range.</summary>
    public IReadOnlyList<ConfigIssue> Issues { get; }

    /// <summary>Things worth saying that did not reject a value: a legacy key in use, a malformed <c>.env</c>.</summary>
    public IReadOnlyList<string> Notes { get; }

    /// <summary>The home, the user file and the project this load read.</summary>
    public ConfigLocations Locations { get; }

    /// <summary>The effective leaf at a dotted key.</summary>
    /// <param name="key">The dotted key.</param>
    /// <param name="entry">The leaf and its source.</param>
    /// <returns>True when the key is a leaf.</returns>
    public bool TryGet(string key, out ConfigEntry entry) =>
        _byKey.TryGetValue(KeyPath.Join(KeyPath.Split(key)), out entry!);

    /// <summary>The value at a dotted key: a leaf, or a whole table.</summary>
    /// <param name="key">The dotted key.</param>
    /// <returns>The value, or null when absent.</returns>
    public object? Find(string key)
    {
        object node = Values;
        foreach (var part in KeyPath.Split(key))
        {
            if (node is not TomlTable table || !table.TryGetValue(part, out node!))
            {
                return null;
            }
        }
        return node;
    }

    /// <summary>The value at a dotted key as JSON (<see cref="ConfigJson.Find"/>), the lookup
    /// behind <c>IHost.ConfigJson</c>.</summary>
    /// <param name="key">The dotted key, such as <c>plugins.inject</c>.</param>
    /// <returns>The element, or null when absent.</returns>
    public System.Text.Json.JsonElement? Json(string key) => ConfigJson.Find(this, key);
}
