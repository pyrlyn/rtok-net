using Pyrlyn.Config.Layered;

namespace Rtok.Config;

/// <summary>A key a file sets to something other than its default: what <c>validate</c> and <c>doctor</c> list.</summary>
/// <param name="Key">The dotted key.</param>
/// <param name="Value">The value the file sets.</param>
/// <param name="Default">The default it differs from.</param>
/// <param name="Source">The file and line.</param>
public sealed record ConfigPin(string Key, object Value, object Default, ConfigSource Source)
{
    /// <summary>The keys files set away from their defaults, in key order.</summary>
    /// <param name="config">The loaded configuration.</param>
    /// <returns>The pins.</returns>
    public static IReadOnlyList<ConfigPin> Of(LoadedConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var pins = new List<ConfigPin>();
        foreach (var entry in config.Entries)
        {
            if (
                entry.Source.Kind == ConfigSourceKind.File
                && ConfigSchema.DefaultOf(entry.Key) is { } fallback
                && !string.Equals(
                    TomlText.Format(entry.Value),
                    TomlText.Format(
                        ConfigPaths.Expand(
                            entry with
                            {
                                Value = fallback,
                            },
                            config.Locations,
                            _ => null
                        )
                    ),
                    StringComparison.Ordinal
                )
            )
            {
                pins.Add(new ConfigPin(entry.Key, entry.Value, fallback, entry.Source));
            }
        }
        return pins;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"{ConfigIssue.Locate(Source)}: {Key} = {ConfigDisplay.Show(Key, Value)} (default {ConfigDisplay.Show(Key, Default)})";
}
