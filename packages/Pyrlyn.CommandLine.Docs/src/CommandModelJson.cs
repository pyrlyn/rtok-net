using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pyrlyn.CommandLine.Docs;

/// <summary>
/// The model as stable JSON: two-space indent, <c>\n</c> line ends, a final newline, and no
/// escaping beyond what JSON needs, so a checked-in copy diffs line by line.
/// </summary>
public static class CommandModelJson
{
    private static readonly JsonSerializerOptions Options = new(ModelJsonContext.Default.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Writes the model.</summary>
    /// <param name="model">The model.</param>
    /// <returns>The JSON text.</returns>
    public static string Serialize(CommandModel model) =>
        JsonSerializer.Serialize(model, Typed) + "\n";

    /// <summary>Reads a model written by <see cref="Serialize"/>.</summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The model.</returns>
    /// <exception cref="JsonException">The text is not a command model.</exception>
    public static CommandModel Deserialize(string json) =>
        JsonSerializer.Deserialize(json, Typed)
        ?? throw new JsonException("the command model is null");

    /// <summary>
    /// The drift check: compares a checked-in model with one read from the current build.
    /// </summary>
    /// <param name="expectedJson">The checked-in JSON.</param>
    /// <param name="actual">The model read now.</param>
    /// <returns>Null when they match; otherwise the first differing line of each.</returns>
    public static string? Diff(string expectedJson, CommandModel actual)
    {
        ArgumentNullException.ThrowIfNull(expectedJson);
        var want = expectedJson.ReplaceLineEndings("\n").Split('\n');
        var got = Serialize(actual).Split('\n');
        for (var i = 0; i < Math.Max(want.Length, got.Length); i++)
        {
            var w = i < want.Length ? want[i] : "<end>";
            var g = i < got.Length ? got[i] : "<end>";
            if (!string.Equals(w, g, StringComparison.Ordinal))
            {
                return $"command model drift at line {i + 1}:\n- {w}\n+ {g}";
            }
        }
        return null;
    }

    private static System.Text.Json.Serialization.Metadata.JsonTypeInfo<CommandModel> Typed =>
        (System.Text.Json.Serialization.Metadata.JsonTypeInfo<CommandModel>)
            Options.GetTypeInfo(typeof(CommandModel));
}
