using System.Text.Json.Serialization;

namespace Rtok.Plugin.Sdk.Tests;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(Cfg))]
internal sealed partial class TestsJsonContext : JsonSerializerContext;
