using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Pyrlyn.Config.Layered;
using Tomlyn.Model;

namespace Rtok.Config.Tests;

/// <summary>
/// Renders <c>config/default.toml</c> from the schema: every key at its default, commented out,
/// with the property's doc comment beside it. The checked-in file must equal this.
/// </summary>
internal static partial class ReferenceConfig
{
    private const int HeaderColumn = 38;
    private const int RowColumn = 40;
    private const int MaxPaddedKey = 12;

    private static readonly string[] Header =
    [
        "# rtok configuration. Flags and RTOK_* environment variables override it.",
        "# Precedence: defaults < this file < <git root>/.rtok.toml < .env < env < flags.",
        "# Check: `rtok config validate`. Where a value came from: `rtok config show --sources`.",
        "# Assignments are commented out so a key left untouched follows the current default,",
        "# not the default from the day this file was written. Uncomment a line to pin it.",
    ];

    private static readonly Lazy<Dictionary<string, string>> Docs = new(LoadDocs);

    public static string Render()
    {
        var text = new StringBuilder();
        foreach (var line in Header)
        {
            text.Append(line).Append('\n');
        }
        foreach (var (key, value) in ConfigSchema.Defaults)
        {
            var property = Property(typeof(RtokConfig), key);
            Table(text, [key], (TomlTable)value, property);
        }
        return text.ToString();
    }

    private static void Table(
        StringBuilder text,
        string[] path,
        TomlTable table,
        PropertyInfo property
    )
    {
        var type = property.PropertyType;
        var rows = table.Where(p => p.Value is not TomlTable).ToList();
        var children = table.Where(p => p.Value is TomlTable).ToList();
        var mapOf = MapValueType(type);
        if (rows.Count > 0 || children.Count == 0 || mapOf == typeof(string))
        {
            text.Append('\n');
            Pad(text, $"[{KeyPath.Join(path)}]", HeaderColumn, TableDoc(property));
            Rows(text, rows, mapOf is null ? type : null);
        }
        foreach (var (key, value) in children)
        {
            var child = (TomlTable)value;
            if (mapOf is not null)
            {
                text.Append('\n').Append($"# [{KeyPath.Join([.. path, key])}]").Append('\n');
                Rows(text, [.. child], mapOf);
            }
            else
            {
                Table(text, [.. path, key], child, Property(type, key));
            }
        }
    }

    private static void Rows(
        StringBuilder text,
        List<KeyValuePair<string, object>> rows,
        Type? owner
    )
    {
        var width = rows.Select(r => KeyPath.Segment(r.Key).Length)
            .Where(n => n <= MaxPaddedKey)
            .DefaultIfEmpty(0)
            .Max();
        foreach (var (key, value) in rows)
        {
            var name = KeyPath.Segment(key);
            var row = $"# {name.PadRight(width)} = {TomlText.Format(value)}";
            Pad(text, row, RowColumn, owner is null ? null : Doc(Property(owner, key)));
        }
    }

    private static void Pad(StringBuilder text, string line, int column, string? doc)
    {
        text.Append(line);
        if (!string.IsNullOrEmpty(doc))
        {
            text.Append(' ', Math.Max(1, column - line.Length)).Append("# ").Append(doc);
        }
        text.Append('\n');
    }

    /// <summary>The header comment: the property's own doc, unless it only names the table; then the type's.</summary>
    private static string? TableDoc(PropertyInfo property)
    {
        var own = Doc(property);
        if (own is not null && !own.StartsWith("The [", StringComparison.Ordinal))
        {
            return own;
        }
        var type = MapValueType(property.PropertyType) is null ? property.PropertyType : null;
        return type is null ? own : Docs.Value.GetValueOrDefault($"T:{type.FullName}");
    }

    private static string? Doc(PropertyInfo property) =>
        Docs.Value.GetValueOrDefault($"P:{property.DeclaringType!.FullName}.{property.Name}");

    private static PropertyInfo Property(Type type, string key) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Single(p => string.Equals(TomlName(p), key, StringComparison.Ordinal));

    private static string TomlName(PropertyInfo p) =>
        p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
        ?? JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name);

    private static Type? MapValueType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>)
            ? type.GetGenericArguments()[1]
            : null;

    private static Dictionary<string, string> LoadDocs()
    {
        var file = Path.ChangeExtension(typeof(RtokConfig).Assembly.Location, ".xml");
        return XDocument
            .Load(file)
            .Descendants("member")
            .Where(m => m.Element("summary") is not null)
            .ToDictionary(
                m => (string)m.Attribute("name")!,
                m => Flatten(m.Element("summary")!),
                StringComparer.Ordinal
            );
    }

    private static string Flatten(XElement summary) => Space().Replace(summary.Value, " ").Trim();

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Space();
}
