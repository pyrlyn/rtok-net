using System.Text;
using Pyrlyn.Config.Layered;
using Tomlyn;

namespace Rtok.Config;

/// <summary>
/// Turns checked leaves into <see cref="RtokConfig"/> through the source-generated serializer.
/// A leaf with an issue is left out, so its property keeps the default.
/// </summary>
internal static class ConfigBinder
{
    /// <summary>Binds the leaves. A value the serializer still rejects (a number past its type) becomes an issue.</summary>
    public static RtokConfig Bind(IReadOnlyList<ConfigEntry> entries, List<ConfigIssue> issues)
    {
        var rejected = issues.Select(i => i.Key).ToHashSet(StringComparer.Ordinal);
        var known = new HashSet<string>(ConfigSchema.LeafKeys, StringComparer.Ordinal);
        // Each round drops one more leaf; the defaults alone always bind.
        while (true)
        {
            var lines = entries
                .Where(e => !rejected.Contains(e.Key) && Bindable(e.Key, known))
                .ToList();
            var text = new StringBuilder();
            foreach (var e in lines)
            {
                text.Append(e.Key).Append(" = ").Append(TomlText.Format(e.Value)).Append('\n');
            }
            try
            {
                return TomlSerializer.Deserialize(
                        text.ToString(),
                        RtokConfigContext.Default.RtokConfig
                    ) ?? new RtokConfig();
            }
            catch (TomlException e) when (e.Line is { } line && line >= 1 && line <= lines.Count)
            {
                var bad = lines[e.Line!.Value - 1];
                rejected.Add(bad.Key);
                issues.Add(
                    new ConfigIssue(
                        bad.Key,
                        ConfigIssue.Locate(bad.Source),
                        $"{bad.Key}: out of range"
                    )
                );
            }
        }
    }

    /// <summary>Known leaves, and entries of the open maps (prices, configs, extensions).</summary>
    private static bool Bindable(string key, HashSet<string> known) =>
        known.Contains(key)
        || ConfigSchema.Lookup(KeyPath.Split(key), out var open) is not null && open;
}
