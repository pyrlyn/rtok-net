using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Pyrlyn.ChangePreview;

/// <summary>
/// The preview for <c>--json</c>: <c>{"edits":[{"path","insertions","deletions"}],"removals":[{"path","bytes","files"}]}</c>.
/// The diff text stays out.
/// </summary>
public static class PreviewJson
{
    private static readonly JsonSerializerOptions Options = new(PreviewJsonContext.Default.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Writes the preview as compact JSON.</summary>
    /// <param name="preview">The preview.</param>
    /// <returns>The JSON text.</returns>
    public static string Serialize(Preview preview) =>
        JsonSerializer.Serialize(
            preview,
            (JsonTypeInfo<Preview>)Options.GetTypeInfo(typeof(Preview))
        );
}
