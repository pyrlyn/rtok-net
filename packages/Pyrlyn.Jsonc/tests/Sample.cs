using System.Text;
using System.Text.Json.Nodes;

namespace Pyrlyn.Jsonc.Tests;

/// <summary>
/// A random JSONC document: the value it holds, and where each entry and container sits in the
/// text. Styles vary per container: on lines or inline, trailing commas, block and line comments,
/// two spaces, four or a tab, LF or CRLF.
/// </summary>
internal sealed class Sample
{
    private static readonly JsonNode?[] Leaves =
    [
        0,
        -17,
        1.5,
        "",
        "é ü",
        "a\"b\\c",
        "/* no */",
        true,
        false,
        null,
    ];

    private readonly Random random;
    private readonly StringBuilder text = new();
    private readonly string unit;
    private readonly string newLine;
    private int comments;

    /// <summary>An entry: the gap it owns between its neighbours, and its value.</summary>
    public sealed record Slot(JsoncSegment[] Path, int GapStart, int ValueStart, int ValueEnd)
    {
        public int GapEnd { get; set; }
    }

    /// <summary>A container: where a new entry may go, from after its last value to its closing bracket.</summary>
    public sealed record Box(JsoncSegment[] Path, bool IsObject, int Count, int After, int Close);

    public Sample(int seed)
    {
        random = new Random(seed);
        unit = random.Next(3) switch
        {
            0 => "  ",
            1 => "    ",
            _ => "\t",
        };
        newLine = random.Next(3) == 0 ? "\r\n" : "\n";
        Model = Tree(0, isObject: true)!;
        Value(Model, [], "", lines: random.Next(4) != 0);
        if (random.Next(2) == 0)
        {
            text.Append(newLine);
        }
        Text = text.ToString();
    }

    public JsonNode Model { get; }

    public string Text { get; }

    public List<Slot> Slots { get; } = [];

    public List<Box> Boxes { get; } = [];

    public JsonNode? Leaf() => Leaves[random.Next(Leaves.Length)]?.DeepClone();

    public JsonNode? Tree(int depth, bool? isObject = null)
    {
        var kind = isObject switch
        {
            true => 0,
            false => 1,
            null => depth > 3 ? 2 : random.Next(4),
        };
        if (kind >= 2)
        {
            return Leaf();
        }
        var count = random.Next(depth == 0 ? 1 : 0, 5);
        if (kind == 1)
        {
            var array = new JsonArray();
            for (var i = 0; i < count; i++)
            {
                array.Add(Tree(depth + 1));
            }
            return array;
        }
        var obj = new JsonObject();
        for (var i = 0; i < count; i++)
        {
            obj[random.Next(5) == 0 ? $"k{i}.\"é\"" : $"k{i}"] = Tree(depth + 1);
        }
        return obj;
    }

    private string Comment() => $"/*c{comments++}*/";

    private void Value(JsonNode? node, JsoncSegment[] path, string indent, bool lines)
    {
        if (node is not JsonObject and not JsonArray)
        {
            text.Append(node?.ToJsonString() ?? "null");
            return;
        }
        var entries = node is JsonObject o
            ? o.Select(p => (Segment: JsoncSegment.Of(p.Key), Key: (string?)p.Key, p.Value))
                .ToList()
            : node.AsArray()
                .Select((v, i) => (Segment: JsoncSegment.At(i), Key: (string?)null, Value: v))
                .ToList();
        var trailing = entries.Count > 0 && random.Next(4) == 0;
        var inner = indent + unit;
        var open = text.Length;
        text.Append(node is JsonObject ? '{' : '[');
        var after = open + 1;
        Slot? previous = null;
        for (var i = 0; i < entries.Count; i++)
        {
            var (segment, key, value) = entries[i];
            Before(lines, inner, first: i == 0);
            if (previous is not null)
            {
                previous.GapEnd = text.Length;
            }
            if (key is not null)
            {
                text.Append(JsonValue.Create(key).ToJsonString()).Append(": ");
            }
            var valueStart = text.Length;
            Value(value, [.. path, segment], inner, lines && random.Next(3) != 0);
            var slot = new Slot([.. path, segment], after, valueStart, text.Length);
            after = text.Length;
            Slots.Add(slot);
            previous = slot;
            After(lines, comma: i < entries.Count - 1 || trailing);
        }
        if (lines)
        {
            if (random.Next(6) == 0)
            {
                text.Append(newLine).Append(inner).Append(Comment());
            }
            text.Append(newLine).Append(indent);
        }
        if (previous is not null)
        {
            previous.GapEnd = text.Length;
        }
        Boxes.Add(new Box(path, node is JsonObject, entries.Count, after, text.Length));
        text.Append(node is JsonObject ? '}' : ']');
    }

    /// <summary>What goes before an entry: its line and indent, or a space, maybe a comment.</summary>
    private void Before(bool lines, string inner, bool first)
    {
        if (lines)
        {
            if (random.Next(5) == 0)
            {
                text.Append(newLine).Append(inner).Append("// c").Append(comments++);
            }
            text.Append(newLine).Append(inner);
        }
        else if (random.Next(4) == 0)
        {
            text.Append(' ').Append(Comment()).Append(' ');
        }
        else if (!first)
        {
            text.Append(' ');
        }
    }

    /// <summary>What goes after a value: maybe a comment, the comma, maybe a line comment.</summary>
    private void After(bool lines, bool comma)
    {
        if (random.Next(5) == 0)
        {
            text.Append(' ').Append(Comment());
        }
        if (comma)
        {
            text.Append(',');
        }
        if (lines && random.Next(4) == 0)
        {
            text.Append(" // c").Append(comments++);
        }
    }
}
