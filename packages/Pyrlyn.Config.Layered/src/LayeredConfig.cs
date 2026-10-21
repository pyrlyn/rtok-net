using Tomlyn.Model;

namespace Pyrlyn.Config.Layered;

/// <summary>The merged result of a <see cref="LayeredConfigBuilder"/>: one tree and the source of every leaf.</summary>
public sealed class LayeredConfig
{
    internal LayeredConfig(
        TomlTable values,
        IReadOnlyList<ConfigEntry> entries,
        IReadOnlyList<string> warnings
    )
    {
        Values = values;
        Entries = entries;
        Warnings = warnings;
    }

    /// <summary>The merged tree. Tables merge key by key; a leaf (scalar or array) is replaced whole.</summary>
    public TomlTable Values { get; }

    /// <summary>Every effective leaf with its source, sorted by key.</summary>
    public IReadOnlyList<ConfigEntry> Entries { get; }

    /// <summary>Problems that did not stop the merge, such as a malformed <c>.env</c> file.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>The effective leaf at a dotted key.</summary>
    /// <param name="key">The dotted key.</param>
    /// <param name="entry">The leaf and its source.</param>
    /// <returns>True when the key is a leaf.</returns>
    public bool TryGet(string key, out ConfigEntry entry)
    {
        var path = KeyPath.Join(KeyPath.Split(key));
        var i = BinarySearch(path);
        entry = i >= 0 ? Entries[i] : null!;
        return i >= 0;
    }

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

    private int BinarySearch(string key)
    {
        int lo = 0,
            hi = Entries.Count - 1;
        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) / 2);
            var c = string.CompareOrdinal(Entries[mid].Key, key);
            if (c == 0)
            {
                return mid;
            }
            if (c < 0)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return -1;
    }
}
