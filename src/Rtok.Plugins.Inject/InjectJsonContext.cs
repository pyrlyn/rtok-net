using System.Text.Json.Serialization;

namespace Rtok.Plugins.Inject;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(InjectConfig))]
[JsonSerializable(typeof(SetupConfig))]
internal sealed partial class InjectJsonContext : JsonSerializerContext;
