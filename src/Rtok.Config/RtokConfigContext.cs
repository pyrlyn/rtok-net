using System.Text.Json.Serialization;
using Tomlyn;
using Tomlyn.Serialization;

namespace Rtok.Config;

/// <summary>Source-generated TOML metadata for <see cref="RtokConfig"/>: snake_case keys, no reflection.</summary>
[TomlSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = TomlIgnoreCondition.WhenWritingNull
)]
[TomlSerializable(typeof(RtokConfig))]
[TomlSerializable(typeof(ModelPrice))]
internal sealed partial class RtokConfigContext : TomlSerializerContext;
