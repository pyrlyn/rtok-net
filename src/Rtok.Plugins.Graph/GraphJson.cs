using System.Text.Json.Serialization;
using Rtok.Config;

namespace Rtok.Plugins.Graph;

/// <summary>Source-generated JSON for <c>[plugins.graph]</c> as the host hands it over:
/// snake_case keys, the same <see cref="GraphSection"/> the TOML schema defines; and the
/// co-change document the plugin keeps in its state.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(GraphSection))]
[JsonSerializable(typeof(CochangeDoc))]
internal sealed partial class GraphJson : JsonSerializerContext;
