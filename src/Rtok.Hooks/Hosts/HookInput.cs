using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;

namespace Rtok.Hooks.Hosts;

/// <summary>
/// One hook payload in Claude's snake_case shape, the one the dispatch reads (rtok
/// <c>HookInput</c>). The host adapters (<see cref="HostAdapters"/>) bring the other hosts'
/// envelopes to it. Keys rtok does not know stay where they are and round-trip.
/// </summary>
public sealed class HookInput
{
    // The keys rtok reads as text: a payload that sends one as anything else is not a payload.
    private static readonly string[] TextKeys =
    [
        "session_id",
        "agent_id",
        "agent_type",
        "task_description",
        "hook_event_name",
        "transcript_path",
        "cwd",
        "permission_mode",
        "tool_name",
        "tool_use_id",
        "prompt",
        "source",
        "trigger",
        "custom_instructions",
    ];

    private HookInput(JsonObject fields) => Fields = fields;

    /// <summary>The whole payload.</summary>
    public JsonObject Fields { get; }

    public string SessionId
    {
        get => Text("session_id") ?? "";
        set => Set("session_id", value);
    }

    public string HookEventName
    {
        get => Text("hook_event_name") ?? "";
        set => Set("hook_event_name", value);
    }

    public string? AgentId => Text("agent_id");

    public string? AgentType
    {
        get => Text("agent_type");
        set => Set("agent_type", value);
    }

    public string? TaskDescription
    {
        get => Text("task_description");
        set => Set("task_description", value);
    }

    public string? TranscriptPath
    {
        get => Text("transcript_path");
        set => Set("transcript_path", value);
    }

    public string? Cwd
    {
        get => Text("cwd");
        set => Set("cwd", value);
    }

    public string? PermissionMode
    {
        get => Text("permission_mode");
        set => Set("permission_mode", value);
    }

    public string? ToolName
    {
        get => Text("tool_name");
        set => Set("tool_name", value);
    }

    public JsonNode? ToolInput
    {
        get => Node("tool_input");
        set => SetNode("tool_input", value);
    }

    public string? ToolUseId
    {
        get => Text("tool_use_id");
        set => Set("tool_use_id", value);
    }

    public JsonNode? ToolResponse
    {
        get => Node("tool_response");
        set => SetNode("tool_response", value);
    }

    public string? Prompt
    {
        get => Text("prompt");
        set => Set("prompt", value);
    }

    public string? Source => Text("source");

    public string? Trigger => Text("trigger");

    /// <summary>The payload, or <see langword="null"/> when <paramref name="json"/> is not a
    /// JSON object, is not valid UTF-8, or sends one of rtok's text keys as anything but a
    /// string.</summary>
    public static HookInput? Parse(ReadOnlySpan<byte> json)
    {
        // The reader passes invalid UTF-8 inside a string through; rtok's parser rejects it.
        if (!Utf8.IsValid(json))
        {
            return null;
        }
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
        if (node is not JsonObject fields)
        {
            return null;
        }
        foreach (var key in TextKeys)
        {
            if (
                fields.TryGetPropertyValue(key, out var value)
                && value is not null
                && value.GetValueKind() != JsonValueKind.String
            )
            {
                return null;
            }
        }
        return new HookInput(fields);
    }

    /// <summary>The value at <paramref name="key"/> if it is a string.</summary>
    public string? Text(string key) =>
        Fields.TryGetPropertyValue(key, out var v)
        && v is JsonValue value
        && value.TryGetValue<string>(out var s)
            ? s
            : null;

    /// <summary>Removes <paramref name="key"/> and returns what it held.</summary>
    public JsonNode? Take(string key)
    {
        if (!Fields.TryGetPropertyValue(key, out var v))
        {
            return null;
        }
        Fields.Remove(key);
        return v;
    }

    /// <summary>Removes <paramref name="key"/> and returns it if it held a string.</summary>
    public string? TakeText(string key) =>
        Take(key) is JsonValue value && value.TryGetValue<string>(out var s) ? s : null;

    /// <summary>The payload as JSON, in its own key order.</summary>
    public byte[] ToJson() => HookJson.Bytes(Fields);

    private JsonNode? Node(string key) => Fields.TryGetPropertyValue(key, out var v) ? v : null;

    private void Set(string key, string? value)
    {
        if (value is null)
        {
            Fields.Remove(key);
        }
        else
        {
            Fields[key] = value;
        }
    }

    private void SetNode(string key, JsonNode? value)
    {
        if (value is null)
        {
            Fields.Remove(key);
            return;
        }
        // A node lives in one tree: detach it from where an adapter found it.
        if (value.Parent is JsonObject parent && value.GetPropertyName() is { } name)
        {
            parent.Remove(name);
        }
        Fields[key] = value;
    }
}
