using System.Text.Json.Serialization;

namespace Rtok.Plugins.Cmd.Tests;

[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(int))]
internal sealed partial class TestJson : JsonSerializerContext;
