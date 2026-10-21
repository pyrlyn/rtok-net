using System.Text.Json.Serialization;

namespace Rtok.SamplePlugin;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(ShrinkConfig))]
internal sealed partial class SampleJsonContext : JsonSerializerContext;
