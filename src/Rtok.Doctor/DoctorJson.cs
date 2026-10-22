using System.Text.Json.Serialization;

namespace Rtok.Doctor;

/// <summary>The source-generated JSON of <c>rtok doctor --json</c>: AOT-safe, snake_case, nulls left out.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true
)]
[JsonSerializable(typeof(DoctorReport))]
public sealed partial class DoctorJson : JsonSerializerContext;
