namespace Pyrlyn.Jsonc;

/// <summary>
/// One member of an object (with its key) or element of an array (<see cref="Key"/> null,
/// <see cref="KeyStart"/> the value's start). <see cref="Comma"/> is the offset of the comma
/// after it, or -1.
/// </summary>
internal sealed class Entry(string? key, int keyStart, Node value)
{
    public string? Key { get; } = key;
    public int KeyStart { get; } = keyStart;
    public Node Value { get; } = value;
    public int Comma { get; set; } = -1;
}
