using System.Text.Json.Serialization;

namespace Rtok.Tasks.Tests;

[JsonSerializable(typeof(TaskId))]
internal sealed partial class TaskIdJson : JsonSerializerContext;
