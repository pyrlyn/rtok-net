using System.Text.Json;
using Pyrlyn.Config.Layered;
using Tomlyn.Model;

namespace Rtok.Config;

/// <summary>Config values as JSON: <c>rtok config show --json</c> and the plugins' sections.</summary>
public static class ConfigJson
{
    /// <summary>Writes one value; tables become objects, arrays arrays.</summary>
    /// <param name="w">The writer.</param>
    /// <param name="key">The value's dotted key, which decides what is redacted.</param>
    /// <param name="value">The value as loaded.</param>
    /// <param name="redact">Hide secrets as <c>config show</c> does.</param>
    public static void Write(Utf8JsonWriter w, string key, object value, bool redact)
    {
        ArgumentNullException.ThrowIfNull(w);
        switch (value)
        {
            case TomlTable table:
                w.WriteStartObject();
                foreach (var (k, v) in table)
                {
                    w.WritePropertyName(k);
                    Write(w, $"{key}.{KeyPath.Segment(k)}", v, redact);
                }
                w.WriteEndObject();
                break;
            case TomlArray or TomlTableArray:
                w.WriteStartArray();
                foreach (var item in (System.Collections.IEnumerable)value)
                {
                    Write(w, key, item!, redact);
                }
                w.WriteEndArray();
                break;
            case bool b:
                w.WriteBooleanValue(b);
                break;
            case long l:
                w.WriteNumberValue(l);
                break;
            case double d:
                w.WriteNumberValue(d);
                break;
            case string s:
                w.WriteStringValue(redact ? ConfigDisplay.Show(key, s) : s);
                break;
            default:
                w.WriteStringValue(
                    redact ? ConfigDisplay.Show(key, value) : Convert.ToString(value, null)
                );
                break;
        }
    }

    /// <summary>The value at <paramref name="key"/> as JSON, unredacted; null when unset or not a key.</summary>
    /// <param name="config">The loaded config.</param>
    /// <param name="key">The dotted key, e.g. <c>plugins.measure</c>.</param>
    /// <returns>The value, or null.</returns>
    public static JsonElement? Find(LoadedConfig config, string key)
    {
        ArgumentNullException.ThrowIfNull(config);
        object? value;
        try
        {
            value = config.Find(key);
        }
        catch (ArgumentException)
        {
            return null;
        }
        if (value is null)
        {
            return null;
        }
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            Write(w, key, value, redact: false);
        }
        using var doc = JsonDocument.Parse(buffer.ToArray());
        return doc.RootElement.Clone();
    }
}
