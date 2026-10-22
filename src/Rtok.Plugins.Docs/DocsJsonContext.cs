using System.Text.Json.Serialization;

namespace Rtok.Plugins.Docs;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(DocsConfig))]
internal sealed partial class DocsJsonContext : JsonSerializerContext;
