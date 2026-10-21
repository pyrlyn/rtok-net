using System.Collections;
using Pyrlyn.Config.Layered;
using Tomlyn.Model;

namespace Rtok.Config;

/// <summary>How <c>rtok config</c> prints values: strings raw, lists and tables in TOML shape, secrets hidden.</summary>
public static class ConfigDisplay
{
    /// <summary>What a set secret prints as.</summary>
    public const string Redacted = "<redacted>";

    /// <summary>A value for people: <c>text</c>, <c>[a, b]</c>, <c>{ k = v }</c>, <c>8080</c>, <c>true</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    public static string Format(object value) =>
        value switch
        {
            string s => s,
            TomlTable table => table.Count == 0
                ? "{}"
                : "{ "
                    + string.Join(
                        ", ",
                        table.Select(p => $"{KeyPath.Segment(p.Key)} = {Format(p.Value)}")
                    )
                    + " }",
            IEnumerable items => "[" + string.Join(", ", items.Cast<object>().Select(Format)) + "]",
            _ => TomlText.Format(value),
        };

    /// <summary>The value of a key as <c>rtok config</c> shows it: a set secret is <see cref="Redacted"/>.</summary>
    /// <param name="key">The dotted key.</param>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    public static string Show(string key, object value) =>
        value switch
        {
            TomlTable table when table.Count > 0 => "{ "
                + string.Join(
                    ", ",
                    table.Select(p =>
                        $"{KeyPath.Segment(p.Key)} = {Show($"{key}.{KeyPath.Segment(p.Key)}", p.Value)}"
                    )
                )
                + " }",
            _ when ConfigSchema.SecretKeys.Contains(key) && value is not string { Length: 0 } =>
                Redacted,
            _ => Format(value),
        };

    /// <summary>Where a value came from, as <c>show --sources</c> prints it.</summary>
    /// <param name="source">The source.</param>
    /// <returns><c>default</c>, <c>user ~/.rtok/config.toml:12</c>, <c>.env ./.env:3</c>, <c>env RTOK_X</c>, <c>flag --x</c>.</returns>
    public static string Source(ConfigSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Kind switch
        {
            ConfigSourceKind.File or ConfigSourceKind.DotEnv =>
                $"{source.Layer} {ConfigIssue.Locate(source)}",
            ConfigSourceKind.Defaults => source.Layer,
            _ => ConfigIssue.Locate(source),
        };
    }
}
