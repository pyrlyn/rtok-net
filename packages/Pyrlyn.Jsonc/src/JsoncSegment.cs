using System.Runtime.InteropServices;

namespace Pyrlyn.Jsonc;

/// <summary>One step of a key path: an object key, or an array index.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct JsoncSegment
{
    private JsoncSegment(string? key, int index)
    {
        Key = key;
        Index = index;
    }

    /// <summary>The object key, or null for an array index.</summary>
    public string? Key { get; }

    /// <summary>The array index, or -1 for an object key.</summary>
    public int Index { get; }

    /// <summary>Whether this step is an array index.</summary>
    public bool IsIndex => Key is null;

    /// <summary>A step into an object.</summary>
    public static JsoncSegment Of(string key) =>
        new(key ?? throw new ArgumentNullException(nameof(key)), -1);

    /// <summary>A step into an array.</summary>
    public static JsoncSegment At(int index) =>
        new(null, index >= 0 ? index : throw new ArgumentOutOfRangeException(nameof(index)));

    /// <summary>An object key.</summary>
    public static implicit operator JsoncSegment(string key) => Of(key);

    /// <summary>An array index.</summary>
    public static implicit operator JsoncSegment(int index) => At(index);

    /// <inheritdoc/>
    public override string ToString() => Key ?? $"[{Index}]";

    /// <summary>The path as <c>a.b[2].c</c>, for messages.</summary>
    public static string Describe(ReadOnlySpan<JsoncSegment> path)
    {
        var parts = new System.Text.StringBuilder();
        foreach (var segment in path)
        {
            if (!segment.IsIndex && parts.Length > 0)
            {
                parts.Append('.');
            }
            parts.Append(segment.ToString());
        }
        return parts.Length == 0 ? "the root" : parts.ToString();
    }
}
