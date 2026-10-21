using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Rtok.Plugins.Cmd;

/// <summary>
/// The JSON rewrite of a command body (rtok T65.2): one line per top-level key or element,
/// nulls and empty values dropped, long arrays and strings cut with their size. Keys print in
/// sorted order and numbers in their shortest form, as rtok's serde_json does.
/// </summary>
internal static class JsonView
{
    private static readonly JsonDocumentOptions Options = new() { MaxDepth = 128 };

    /// <summary>A JSON object or array body, parsed; null for anything else. Primitives stay on
    /// the line-cut path so a one-word command is not rewritten.</summary>
    public static JsonElement? Parse(string text)
    {
        var t = text.Trim();
        if (t.Length == 0 || t[0] is not ('{' or '['))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(t, Options);
            return Valid(doc.RootElement) ? doc.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The body as lines: <c>key: value</c> per object member, one line per array element.</summary>
    public static string Top(JsonElement value, int items, int stringMax)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                return string.Join(
                    '\n',
                    Members(value)
                        .Select(m =>
                            Value(m.Value, items, stringMax) is { } f ? $"{m.Key}: {f}" : null
                        )
                        .OfType<string>()
                );
            case JsonValueKind.Array:
                var all = value.GetArrayLength();
                var lines = value
                    .EnumerateArray()
                    .Take(items)
                    .Select(v => Value(v, items, stringMax))
                    .OfType<string>()
                    .ToList();
                if (all > items)
                {
                    lines.Add($"… +{all - items} more");
                }

                // rtok prints a uniform array as a TOON table when shorter; that encoder is T28's.
                return string.Join('\n', lines);
            default:
                return Value(value, items, stringMax) ?? "";
        }
    }

    /// <summary>A JSON string literal escaped the way serde_json writes it.</summary>
    public static string Quote(string s)
    {
        var output = new StringBuilder(s.Length + 2).Append('"');
        foreach (var c in s)
        {
            _ = c switch
            {
                '"' => output.Append("\\\""),
                '\\' => output.Append("\\\\"),
                '\n' => output.Append("\\n"),
                '\r' => output.Append("\\r"),
                '\t' => output.Append("\\t"),
                '\b' => output.Append("\\b"),
                '\f' => output.Append("\\f"),
                < ' ' => output
                    .Append("\\u00")
                    .Append(((int)c).ToString("x2", CultureInfo.InvariantCulture)),
                _ => output.Append(c),
            };
        }

        return output.Append('"').ToString();
    }

    private static string? Value(JsonElement v, int items, int stringMax)
    {
        switch (v.ValueKind)
        {
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Number:
                return Number(v);
            case JsonValueKind.String:
                var s = v.GetString()!;
                return s.Length == 0 ? null : String(s, stringMax);
            case JsonValueKind.Array:
                var all = v.GetArrayLength();
                if (all == 0)
                {
                    return null;
                }

                var shown = v.EnumerateArray()
                    .Take(items)
                    .Select(e => Value(e, items, stringMax))
                    .OfType<string>();
                var text = $"[{string.Join(", ", shown)}]";
                return all > items ? $"{text} … +{all - items} more" : text;
            case JsonValueKind.Object:
                var fields = Members(v)
                    .Select(m => Value(m.Value, items, stringMax) is { } f ? $"{m.Key}: {f}" : null)
                    .OfType<string>()
                    .ToList();
                return fields.Count == 0 ? null : $"{{{string.Join(", ", fields)}}}";
            default:
                return null;
        }
    }

    private static string String(string s, int max)
    {
        var n = Text.Scalars(s);
        return n <= max ? Quote(s) : $"{Quote(Text.TakeScalars(s, max) + "…")} ({n})";
    }

    // Members in key order, the last of duplicate keys winning (serde_json's BTreeMap).
    private static SortedDictionary<string, JsonElement> Members(JsonElement obj)
    {
        var map = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var p in obj.EnumerateObject())
        {
            map[p.Name] = p.Value;
        }

        return map;
    }

    private static string Number(JsonElement v)
    {
        var raw = v.GetRawText();
        var integer = raw.AsSpan().IndexOfAny('.', 'e', 'E') < 0;
        if (integer && v.TryGetUInt64(out var u))
        {
            return u.ToString(CultureInfo.InvariantCulture);
        }

        if (integer && v.TryGetInt64(out var i))
        {
            return i.ToString(CultureInfo.InvariantCulture);
        }

        return Shortest(v.GetDouble());
    }

    /// <summary>A double in ryu's form: <c>1.0</c>, <c>0.001234</c>, <c>1e30</c>, <c>1.5e-7</c>.</summary>
    internal static string Shortest(double d)
    {
        var sign = double.IsNegative(d) ? "-" : "";
        var (digits, kk) = Decompose(Math.Abs(d).ToString("R", CultureInfo.InvariantCulture));
        if (digits.Length == 0)
        {
            return sign + "0.0";
        }

        // 10^(kk-1) <= |d| < 10^kk
        var length = digits.Length;
        var body = kk switch
        {
            _ when length <= kk && kk <= 16 => digits + new string('0', kk - length) + ".0",
            > 0 and <= 16 => digits[..kk] + "." + digits[kk..],
            > -5 and <= 0 => "0." + new string('0', -kk) + digits,
            _ when length == 1 => $"{digits}e{kk - 1}",
            _ => $"{digits[0]}.{digits[1..]}e{kk - 1}",
        };
        return sign + body;
    }

    // The significant digits of a round-trip string ("1.25E-07", "123.45", "0.001") and the
    // decimal exponent kk of the digit after the last integer place.
    private static (string Digits, int Kk) Decompose(string r)
    {
        var e = r.IndexOf('E', StringComparison.Ordinal);
        var mantissa = e < 0 ? r : r[..e];
        var exponent = e < 0 ? 0 : int.Parse(r.AsSpan(e + 1), CultureInfo.InvariantCulture);
        var dot = mantissa.IndexOf('.', StringComparison.Ordinal);
        var whole = dot < 0 ? mantissa : mantissa[..dot];
        var all = whole + (dot < 0 ? "" : mantissa[(dot + 1)..]);
        var lead = all.Length - all.TrimStart('0').Length;
        var digits = all.TrimStart('0').TrimEnd('0');
        return (digits, whole.Length - lead + exponent);
    }

    // serde_json rejects lone surrogates and numbers out of f64 range; so does this view.
    private static bool Valid(JsonElement e)
    {
        try
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.String:
                    _ = e.GetString();
                    return true;
                case JsonValueKind.Number:
                    return e.TryGetDouble(out var d) && double.IsFinite(d);
                case JsonValueKind.Array:
                    return e.EnumerateArray().All(Valid);
                case JsonValueKind.Object:
                    return e.EnumerateObject().All(p => p.Name is not null && Valid(p.Value));
                default:
                    return true;
            }
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
