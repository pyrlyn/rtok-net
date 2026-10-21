using System.Text.Json.Serialization;

namespace Rtok.AotTests;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Probe))]
internal sealed partial class AotTestsJsonContext : JsonSerializerContext;
