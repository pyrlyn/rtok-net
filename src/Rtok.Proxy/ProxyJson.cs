using System.Text.Json.Serialization;

namespace Rtok.Proxy;

/// <summary>The proxy's JSON: snake_case, as rtok wrote it.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(IReadOnlyList<LiveCall>))]
internal sealed partial class ProxyJson : JsonSerializerContext;
