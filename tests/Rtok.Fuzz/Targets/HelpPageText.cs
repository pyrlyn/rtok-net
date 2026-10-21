using System.Text.Json;
using Pyrlyn.CommandLine.Docs;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// <c>--help</c> pages (NUL-separated: the root, then one per command it lists) through
/// <see cref="CommandModelReader"/>: reading fails only with <see cref="HelpFormatException"/>;
/// a model serializes back to itself and renders man pages and completions the same each time.
/// Any text as model JSON fails only with <see cref="JsonException"/>.
/// </summary>
internal static class HelpPageText
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        var text = FuzzInput.Text(data);
        var pages = text.Split('\0', 32);
        var call = 0;
        try
        {
            Check(CommandModelReader.Read("fuzz", _ => call < pages.Length ? pages[call++] : ""));
        }
        catch (HelpFormatException) { }
        try
        {
            _ = CommandModelJson.Serialize(CommandModelJson.Deserialize(text));
        }
        catch (JsonException) { }
    }

    private static void Check(CommandModel model)
    {
        var json = CommandModelJson.Serialize(model);
        FuzzInput.Check(
            string.Equals(
                CommandModelJson.Serialize(CommandModelJson.Deserialize(json)),
                json,
                StringComparison.Ordinal
            ),
            "model JSON does not round-trip"
        );
        FuzzInput.Check(
            CommandModelJson.Diff(json, model) is null,
            "a model differs from its own JSON"
        );
        FuzzInput.Check(
            ManPages.Render(model).SequenceEqual(ManPages.Render(model))
                && CompletionScripts.Render(model).SequenceEqual(CompletionScripts.Render(model)),
            "rendering is not deterministic"
        );
    }
}
