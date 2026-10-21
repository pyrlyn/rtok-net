using System.Text.Json;

namespace Pyrlyn.Jsonc;

/// <summary>Builds the span tree with <see cref="Utf8JsonReader"/>, which also decides what is valid JSONC.</summary>
internal static class Syntax
{
    private static readonly JsonReaderOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        MaxDepth = 256,
    };

    /// <summary>The root value, or null for a document of only whitespace and comments.</summary>
    /// <exception cref="JsonException">The text is not JSONC.</exception>
    public static Node? Parse(ReadOnlySpan<byte> text, int bom)
    {
        if (Bytes.Skip(text, bom) == text.Length)
        {
            return null;
        }
        var reader = new Utf8JsonReader(text[bom..], Options);
        reader.Read();
        var root = Value(ref reader, text, bom);
        if (reader.Read())
        {
            throw new JsonException(
                "'"
                    + (char)text[bom + (int)reader.TokenStartIndex]
                    + "' after the end of the document"
            );
        }
        return root;
    }

    private static Node Value(ref Utf8JsonReader reader, ReadOnlySpan<byte> text, int bom)
    {
        var node = new Node(reader.TokenType, bom + (int)reader.TokenStartIndex);
        if (node.IsObject)
        {
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var key = reader.GetString()!;
                var keyStart = bom + (int)reader.TokenStartIndex;
                reader.Read();
                Add(node, new Entry(key, keyStart, Value(ref reader, text, bom)), text);
            }
        }
        else if (node.IsArray)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                var item = Value(ref reader, text, bom);
                Add(node, new Entry(null, item.Start, item), text);
            }
        }
        node.End = bom + (int)reader.BytesConsumed;
        return node;
    }

    private static void Add(Node parent, Entry entry, ReadOnlySpan<byte> text)
    {
        var next = Bytes.Skip(text, entry.Value.End);
        if (next < text.Length && text[next] == (byte)',')
        {
            entry.Comma = next;
        }
        parent.Entries.Add(entry);
    }
}
