using System.Text.Json.Serialization;
using Rtok.Config;

namespace Rtok.Plugins.Cmd;

/// <summary><c>[plugins.cmd]</c> as the host hands it over: snake_case JSON.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(CmdSection))]
internal sealed partial class CmdJson : JsonSerializerContext;
