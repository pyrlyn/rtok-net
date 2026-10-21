using System.Text.Json.Serialization;

namespace Pyrlyn.ChangePreview;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Preview))]
internal sealed partial class PreviewJsonContext : JsonSerializerContext;
