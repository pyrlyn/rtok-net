using System.Text.Json.Serialization;

namespace Pyrlyn.CommandLine.Docs;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    IndentSize = 2,
    NewLine = "\n"
)]
[JsonSerializable(typeof(CommandModel))]
internal sealed partial class ModelJsonContext : JsonSerializerContext;
