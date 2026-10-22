using System.Text.Json.Serialization;

namespace Rtok.Tasks;

/// <summary>Where a task is. Open and in-progress tasks are the plan; done and closed ones left it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TaskState>))]
public enum TaskState
{
    /// <summary>Not started.</summary>
    [JsonStringEnumMemberName("open")]
    Open,

    /// <summary>Claimed and being worked on.</summary>
    [JsonStringEnumMemberName("in-progress")]
    InProgress,

    /// <summary>Finished.</summary>
    [JsonStringEnumMemberName("done")]
    Done,

    /// <summary>Dropped without being done.</summary>
    [JsonStringEnumMemberName("closed")]
    Closed,
}
