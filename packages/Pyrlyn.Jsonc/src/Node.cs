using System.Text.Json;

namespace Pyrlyn.Jsonc;

/// <summary>A value's bytes: <c>[Start, End)</c> in the document. Objects and arrays hold their entries.</summary>
internal sealed class Node(JsonTokenType kind, int start)
{
    public JsonTokenType Kind { get; } = kind;
    public int Start { get; } = start;
    public int End { get; set; }
    public List<Entry> Entries { get; } = [];

    public bool IsObject => Kind == JsonTokenType.StartObject;
    public bool IsArray => Kind == JsonTokenType.StartArray;
}
