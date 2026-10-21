using System.Text.Json.Serialization;

namespace Rtok.Model;

/// <summary>The snapshot as JSON for the web dashboard, source-generated for AOT.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Snapshot))]
public sealed partial class SnapshotJsonContext : JsonSerializerContext;
