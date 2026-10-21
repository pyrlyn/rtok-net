using Pyrlyn.Config.Layered;
using Tomlyn.Model;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// <see cref="TomlEditor.Set"/> on any text, key and value (NUL-separated): it fails only with
/// <see cref="ConfigFileException"/> or, for a bad key or value, <see cref="ArgumentException"/>;
/// the result parses, holds the value at the key, and setting it again changes nothing.
/// </summary>
internal static class TomlEdit
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        var parts = FuzzInput.Parts(data, 3);
        var (text, key) = (parts[0], parts[1]);
        try
        {
            _ = TomlText.Parse(text);
        }
        catch (ConfigFileException) { }

        var value = TomlText.ParseValueOrString(parts[2]);
        string edited;
        try
        {
            edited = TomlEditor.Set(text, key, value);
        }
        catch (Exception e) when (e is ConfigFileException or ArgumentException)
        {
            return;
        }
        object? found = TomlText.Parse(edited);
        foreach (var part in KeyPath.Split(key))
        {
            found = found is TomlTable table && table.TryGetValue(part, out var next) ? next : null;
        }
        FuzzInput.Check(
            found is not null
                && string.Equals(
                    TomlText.Format(found),
                    TomlText.Format(value),
                    StringComparison.Ordinal
                ),
            $"{key} does not hold the value set"
        );
        FuzzInput.Check(
            string.Equals(TomlEditor.Set(edited, key, value), edited, StringComparison.Ordinal),
            $"setting {key} again changed the text"
        );
    }
}
