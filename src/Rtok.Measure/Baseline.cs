using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Rtok.Measure;

/// <summary>
/// <c>rtok stats --save-baseline</c> and <c>--compare</c>: a report's JSON kept under
/// <c>&lt;home&gt;/measurements/&lt;name&gt;.json</c>, and per-field deltas against it.
/// Both sides are read from the same JSON, so a compare right after a save is all zeros.
/// </summary>
internal static class Baseline
{
    public static string PathOf(string home, string name) =>
        Path.Combine(home, "measurements", name + ".json");

    /// <summary>Writes <paramref name="json"/> and returns its path; a name with a folder in it is refused.</summary>
    public static string Save(string home, string name, string json)
    {
        if (
            name.Length == 0
            || name.Contains('/', StringComparison.Ordinal)
            || name.Contains('\\', StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException($"bad baseline name '{name}'");
        }
        var path = PathOf(home, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
        return path;
    }

    /// <summary>One line per field, <c>key now Δdelta</c>; a row only one side has counts 0 on the other.</summary>
    public static string Compare(string home, string name, string nowJson)
    {
        var path = PathOf(home, name);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"no baseline '{name}' at {path}");
        }
        using var old = JsonDocument.Parse(File.ReadAllBytes(path));
        using var now = JsonDocument.Parse(nowJson);
        var (a, b) = (old.RootElement, now.RootElement);
        var sb = new StringBuilder();
        foreach (var key in (string[])["sessions", "lines", "usage_input", "usage_output"])
        {
            Line(sb, key, Number(a, key), Number(b, key));
        }
        foreach (
            var (label, key) in (ReadOnlySpan<(string, string)>)
                [("tools", "tools"), ("bash", "bash_families"), ("mcp", "mcp_groups")]
        )
        {
            var (ra, rb) = (Rows(a, key), Rows(b, key));
            foreach (
                var n in ra
                    .Keys.Union(rb.Keys, StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
            )
            {
                foreach (var field in (string[])["est_tokens", "ctt"])
                {
                    Line(
                        sb,
                        $"{label}.{n}.{field}",
                        ra.TryGetValue(n, out var x) ? Number(x, field) : 0,
                        rb.TryGetValue(n, out var y) ? Number(y, field) : 0
                    );
                }
            }
        }
        return sb.ToString();
    }

    private static void Line(StringBuilder sb, string key, long old, long now) =>
        sb.Append(CultureInfo.InvariantCulture, $"{key} {now} Δ{now - old}\n");

    private static long Number(JsonElement o, string key) =>
        o.ValueKind is JsonValueKind.Object
        && o.TryGetProperty(key, out var v)
        && v.ValueKind is JsonValueKind.Number
        && v.TryGetInt64(out var n)
            ? n
            : 0;

    private static Dictionary<string, JsonElement> Rows(JsonElement o, string key) =>
        o.ValueKind is JsonValueKind.Object
        && o.TryGetProperty(key, out var rows)
        && rows.ValueKind is JsonValueKind.Object
            ? rows.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal)
            : new(StringComparer.Ordinal);
}
