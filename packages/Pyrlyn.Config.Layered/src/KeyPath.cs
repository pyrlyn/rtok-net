using Tomlyn.Helpers;
using Tomlyn.Model;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace Pyrlyn.Config.Layered;

/// <summary>
/// Dotted keys in TOML syntax: <c>proxy.port</c>, or <c>graph."my.project".root</c> when a
/// segment is not a bare key.
/// </summary>
public static class KeyPath
{
    /// <summary>Splits a dotted key into its segments.</summary>
    /// <param name="dotted">The key, in TOML dotted-key syntax.</param>
    /// <returns>The segments, unquoted.</returns>
    /// <exception cref="ArgumentException">The text is not a TOML key.</exception>
    public static string[] Split(string dotted)
    {
        ArgumentNullException.ThrowIfNull(dotted);
        if (dotted.AsSpan().ContainsAny('\r', '\n'))
        {
            throw new ArgumentException($"not a key: {dotted}", nameof(dotted));
        }
        var doc = SyntaxParser.Parse($"{dotted} = 0", null, validate: true);
        if (doc.HasErrors || doc.Tables.ChildrenCount != 0 || doc.KeyValues.ChildrenCount != 1)
        {
            throw new ArgumentException($"not a key: {dotted}", nameof(dotted));
        }
        return Parts(doc.KeyValues.GetChild(0)!.Key!);
    }

    /// <summary>Joins segments into a dotted key, quoting the ones that are not bare keys.</summary>
    /// <param name="parts">The segments.</param>
    /// <returns>The dotted key.</returns>
    public static string Join(IEnumerable<string> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return string.Join('.', parts.Select(Segment));
    }

    /// <summary>One segment as TOML writes it: bare when it can be, else a basic string.</summary>
    /// <param name="part">The segment.</param>
    /// <returns>The segment in key syntax.</returns>
    public static string Segment(string part) =>
        BareKeySyntax.IsBareKey(part)
            ? part
            : TomlFormatHelper.ToString(part, TomlPropertyDisplayKind.Default);

    internal static string[] Parts(KeySyntax key)
    {
        var parts = new List<string> { Part(key.Key) };
        foreach (var dot in key.DotKeys)
        {
            parts.Add(Part(dot.Key));
        }
        return [.. parts];
    }

    private static string Part(BareKeyOrStringValueSyntax? key) =>
        key switch
        {
            BareKeySyntax bare => bare.Key?.Text ?? "",
            StringValueSyntax quoted => quoted.Value ?? "",
            _ => key?.ToString()?.Trim() ?? "",
        };

    internal static bool StartsWith(IReadOnlyList<string> path, IReadOnlyList<string> prefix)
    {
        if (prefix.Count > path.Count)
        {
            return false;
        }
        for (var i = 0; i < prefix.Count; i++)
        {
            if (!string.Equals(path[i], prefix[i], StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    internal static bool Equal(IReadOnlyList<string> a, IReadOnlyList<string> b) =>
        a.Count == b.Count && StartsWith(a, b);
}
