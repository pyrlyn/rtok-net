using System.Text.Json.Serialization;

namespace Rtok.Web;

/// <summary>The host's own JSON outside <c>/ws</c>.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Health))]
internal sealed partial class WebJsonContext : JsonSerializerContext;
