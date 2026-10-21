using Pyrlyn.Config.Layered;
using Tomlyn.Model;

namespace Rtok.Config;

/// <summary>Sets and removes values in a tree by dotted key.</summary>
internal static class ConfigTree
{
    public static void Set(TomlTable root, string key, object value)
    {
        var path = KeyPath.Split(key);
        var table = root;
        foreach (var part in path[..^1])
        {
            if (!table.TryGetValue(part, out var node) || node is not TomlTable child)
            {
                child = new TomlTable();
                table[part] = child;
            }
            table = child;
        }
        table[path[^1]] = value;
    }

    /// <summary>Removes a key, then every table it leaves empty.</summary>
    public static void Remove(TomlTable root, string key) => Remove(root, KeyPath.Split(key), 0);

    private static bool Remove(TomlTable table, string[] path, int i)
    {
        if (!table.TryGetValue(path[i], out var node))
        {
            return false;
        }
        if (i == path.Length - 1)
        {
            table.Remove(path[i]);
        }
        else if (node is TomlTable child && Remove(child, path, i + 1) && child.Count == 0)
        {
            table.Remove(path[i]);
        }
        return true;
    }
}
