using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rtok.Tasks;

/// <summary>A <see cref="TaskId"/> is a JSON string.</summary>
public sealed class TaskIdJsonConverter : JsonConverter<TaskId>
{
    /// <inheritdoc/>
    public override TaskId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        try
        {
            return TaskId.Parse(reader.GetString() ?? "");
        }
        catch (TaskException e)
        {
            throw new JsonException(e.Message, e);
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, TaskId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStringValue(value.ToString());
    }
}
