using System.Text.Json;
using System.Text.Json.Nodes;
using Pyrlyn.Jsonc;

namespace Rtok.Agents.Hosts.Hooks;

/// <summary>
/// A host's JSON hooks file under edit: Pyrlyn.Jsonc changes one value at a time and keeps
/// every other byte and comment. Errors name the file and leave it as it is.
/// </summary>
internal sealed class HookJson
{
    private readonly string path;

    /// <exception cref="InvalidDataException">The text is not JSONC.</exception>
    public HookJson(string path, string text)
    {
        this.path = path;
        try
        {
            Doc = JsoncDocument.Parse(text, path);
        }
        catch (JsoncException e)
        {
            throw new InvalidDataException(e.Message, e);
        }
    }

    /// <summary>The document as edited so far.</summary>
    public JsoncDocument Doc { get; private set; }

    /// <summary>The text as edited so far.</summary>
    public string Text => Doc.Text;

    /// <summary>The value at <paramref name="at"/>; null when absent.</summary>
    public JsonElement? Get(params JsoncSegment[] at) => Doc.Get(at);

    /// <summary>The object at <paramref name="at"/>; null when absent.</summary>
    /// <exception cref="InvalidDataException">Something else is there.</exception>
    public JsonElement? Object(params JsoncSegment[] at) =>
        Get(at) switch
        {
            null => null,
            { ValueKind: JsonValueKind.Object } o => o,
            _ => throw Shape(at, "an object"),
        };

    /// <summary>The array at <paramref name="at"/>; null when absent.</summary>
    /// <exception cref="InvalidDataException">Something else is there.</exception>
    public JsonElement? Array(params JsoncSegment[] at) =>
        Get(at) switch
        {
            null => null,
            { ValueKind: JsonValueKind.Array } a => a,
            _ => throw Shape(at, "an array"),
        };

    /// <exception cref="InvalidDataException">Something on the way is not an object or array.</exception>
    public void Set(JsoncSegment[] at, JsonNode? value)
    {
        try
        {
            Doc = Doc.Set(at, value);
        }
        catch (InvalidOperationException e)
        {
            throw new InvalidDataException(
                $"{path}: {e.Message}; rtok leaves the file as it is",
                e
            );
        }
    }

    /// <summary>Appends <paramref name="value"/> to the array at <paramref name="at"/>, made when absent.</summary>
    /// <exception cref="InvalidDataException">Something else is there.</exception>
    public void Append(JsoncSegment[] at, JsonNode value)
    {
        if (Array(at) is { } list)
        {
            Set([.. at, list.GetArrayLength()], value);
        }
        else
        {
            Set(at, new JsonArray(value));
        }
    }

    public void Remove(params JsoncSegment[] at) => Doc = Doc.Remove(at);

    /// <summary>
    /// Removes the hooks at <c>&lt;at&gt;.&lt;event&gt;[entry].hooks[hook]</c>; an entry left with no
    /// hooks goes, and so does an event left with no entries.
    /// </summary>
    public void RemoveHooks(
        JsoncSegment[] at,
        IEnumerable<(string Event, int Entry, int Hook)> hooks
    )
    {
        foreach (var byEvent in hooks.GroupBy(h => h.Event, StringComparer.Ordinal))
        {
            foreach (var byEntry in byEvent.GroupBy(h => h.Entry).OrderByDescending(g => g.Key))
            {
                foreach (var hook in byEntry.Select(h => h.Hook).OrderDescending())
                {
                    Remove([.. at, byEvent.Key, byEntry.Key, "hooks", hook]);
                }
                if (Array([.. at, byEvent.Key, byEntry.Key, "hooks"])?.GetArrayLength() == 0)
                {
                    Remove([.. at, byEvent.Key, byEntry.Key]);
                }
            }
            if (Array([.. at, byEvent.Key])?.GetArrayLength() == 0)
            {
                Remove([.. at, byEvent.Key]);
            }
        }
    }

    /// <summary>Removes the object at <paramref name="at"/>, then each parent, while it is a bare <c>{}</c> with no comment.</summary>
    public void DropEmpty(params JsoncSegment[] at)
    {
        for (var depth = at.Length; depth > 0; depth--)
        {
            var raw = Doc.GetRaw(at.AsSpan(0, depth));
            if (
                raw is null
                || !string.Equals(
                    string.Concat(raw.Where(c => !char.IsWhiteSpace(c))),
                    "{}",
                    StringComparison.Ordinal
                )
            )
            {
                return;
            }
            Doc = Doc.Remove(at.AsSpan(0, depth));
        }
    }

    /// <summary>The string at <paramref name="key"/> of <paramref name="o"/>; null when it is absent or not a string.</summary>
    public static string? Str(JsonElement o, string key) =>
        o.ValueKind == JsonValueKind.Object
        && o.TryGetProperty(key, out var v)
        && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    /// <summary>Whether <paramref name="o"/>'s <paramref name="key"/> is the integer <paramref name="n"/>.</summary>
    public static bool Is(JsonElement o, string key, long n) =>
        o.ValueKind == JsonValueKind.Object
        && o.TryGetProperty(key, out var v)
        && v.ValueKind == JsonValueKind.Number
        && v.TryGetInt64(out var got)
        && got == n;

    /// <summary>The number of members of <paramref name="o"/>; 0 when it is not an object.</summary>
    public static int Count(JsonElement o) =>
        o.ValueKind == JsonValueKind.Object ? o.EnumerateObject().Count() : 0;

    /// <summary>The elements of <paramref name="a"/>; none when it is not an array.</summary>
    public static IEnumerable<JsonElement> Items(JsonElement? a) =>
        a is { ValueKind: JsonValueKind.Array } list ? list.EnumerateArray() : [];

    private InvalidDataException Shape(JsoncSegment[] at, string want) =>
        new($"{path}: {JsoncSegment.Describe(at)} is not {want}; rtok leaves the file as it is");
}
