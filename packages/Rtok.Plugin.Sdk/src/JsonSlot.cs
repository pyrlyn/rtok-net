using System.Text.Json.Nodes;

namespace Rtok.Plugin.Sdk;

/// <summary>A place inside a request body: a property of an object or an element of an array.
/// Setting it replaces the value in the body.</summary>
public sealed class JsonSlot
{
    private readonly JsonObject? _object;
    private readonly string? _key;
    private readonly JsonArray? _array;
    private readonly int _index;

    /// <summary>The property <paramref name="key"/> of <paramref name="owner"/>.</summary>
    public JsonSlot(JsonObject owner, string key)
    {
        _object = owner;
        _key = key;
    }

    /// <summary>The element <paramref name="index"/> of <paramref name="owner"/>.</summary>
    public JsonSlot(JsonArray owner, int index)
    {
        _array = owner;
        _index = index;
    }

    /// <summary>The value in the slot.</summary>
    public JsonNode? Value
    {
        get => _object is not null ? _object[_key!] : _array![_index];
        set
        {
            if (_object is not null)
            {
                _object[_key!] = value;
            }
            else
            {
                _array![_index] = value;
            }
        }
    }
}
