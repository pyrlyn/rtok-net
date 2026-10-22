using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rtok.Hooks.Hosts;

/// <summary>
/// Each host's hook envelope brought to Claude's (<see cref="HookInput"/>), so the dispatch
/// reads one shape (rtok <c>adapt_*</c>). Claude, Codex, CodeBuddy, Qwen, Kimi and ZCode send
/// Claude's shape already and only get the event name filled in.
/// </summary>
public static class HostAdapters
{
    /// <summary>Adapts <paramref name="input"/> in place for <paramref name="host"/>.</summary>
    /// <param name="host">The host id from <c>--host</c> or <c>[hook] host</c>.</param>
    /// <param name="cliEvent">The event <c>rtok hook</c> was called with.</param>
    /// <param name="input">The payload.</param>
    /// <param name="env">The host's environment.</param>
    /// <returns>True when the payload changed shape, so the ledger keeps the adapted JSON
    /// rather than the bytes the host sent.</returns>
    public static bool Adapt(
        string host,
        string cliEvent,
        HookInput input,
        Func<string, string?> env
    )
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(env);
        // Grok sends its own envelope to every hook it imports, whatever --host says.
        if (host is "grok" || env("GROK_HOOK_EVENT") is not null)
        {
            Grok(input, cliEvent);
            return true;
        }
        switch (host)
        {
            case "cursor":
                Cursor(input, cliEvent);
                return true;
            case "copilot":
                Copilot(input, cliEvent);
                return true;
            case "gemini":
                Gemini(input, cliEvent);
                return true;
            case "codewhale":
                input.HookEventName = cliEvent;
                input.Prompt ??= input.TakeText("text");
                return true;
            case "cline":
                Cline(input, cliEvent);
                return true;
            case "devin":
                Devin(input, cliEvent, env("DEVIN_PROJECT_DIR"));
                return true;
            case "commandcode":
                CommandCode(input, cliEvent, env("COMMANDCODE_PROJECT_DIR"));
                return false;
            default:
                if (input.HookEventName.Length == 0)
                {
                    input.HookEventName = cliEvent;
                }
                return false;
        }
    }

    private static void Cursor(HookInput input, string cliEvent)
    {
        input.TranscriptPath ??= input.TakeText("transcriptPath");
        if (cliEvent is "afterMCPExecution" || input.HookEventName is "afterMCPExecution")
        {
            input.HookEventName = "AfterMCPExecution";
            if (input.ToolResponse is null && input.Text("result_json") is { } result)
            {
                input.ToolResponse = result;
            }
        }
        if (input.SessionId.Length == 0 && input.Text("conversation_id") is { } id)
        {
            input.SessionId = id;
        }
        if (input.ToolResponse is null)
        {
            input.ToolResponse =
                TakeJsonish(input, "tool_output") ?? TakeJsonish(input, "result_json");
        }
        if (input.ToolName is not null)
        {
            input.ToolInput ??= new JsonObject();
            input.HookEventName = CursorEvent(cliEvent, input.HookEventName);
            return;
        }
        if (input.Text("command") is not { } command)
        {
            input.HookEventName = CursorEvent(cliEvent, input.HookEventName);
            return;
        }
        input.ToolName = "Bash";
        input.ToolInput = new JsonObject { ["command"] = command };
        if (CursorEvent(cliEvent, input.HookEventName) is "PostToolUse")
        {
            input.HookEventName = "PostToolUse";
            if (input.ToolResponse is null)
            {
                var output = input.Fields["output"] ?? input.Fields["stdout"];
                input.ToolResponse = output?.DeepClone() ?? JsonValue.Create("");
            }
        }
        else
        {
            input.HookEventName = "PreToolUse";
        }
    }

    private static void Copilot(HookInput input, string cliEvent)
    {
        if (input.SessionId.Length == 0 && input.TakeText("sessionId") is { } id)
        {
            input.SessionId = id;
        }
        if (input.ToolName is null && input.TakeText("toolName") is { } name)
        {
            input.ToolName = ToolNames.Canonical(name);
        }
        if (input.ToolInput is null && input.Take("toolArgs") is { } args)
        {
            input.ToolInput = DecodeToolArgs(args);
        }
        if (input.ToolResponse is null && input.Take("toolResult") is { } result)
        {
            input.ToolResponse = result;
        }
        if (input.AgentType is null && input.TakeText("agentName") is { } agent)
        {
            input.AgentType = agent;
        }
        if (input.TaskDescription is null && input.TakeText("agentDescription") is { } what)
        {
            input.TaskDescription = what;
        }
        var eventName = input.HookEventName.Length == 0 ? cliEvent : input.HookEventName;
        input.HookEventName = eventName switch
        {
            "preToolUse" => "PreToolUse",
            "postToolUse" => "PostToolUse",
            "sessionStart" => "SessionStart",
            "sessionEnd" => "SessionEnd",
            "userPromptSubmitted" => "UserPromptSubmit",
            "preCompact" => "PreCompact",
            "subagentStart" => "SubagentStart",
            _ => eventName,
        };
    }

    private static void Gemini(HookInput input, string cliEvent)
    {
        var eventName = input.HookEventName.Length == 0 ? cliEvent : input.HookEventName;
        input.HookEventName = eventName switch
        {
            "BeforeTool" => "PreToolUse",
            "AfterTool" => "PostToolUse",
            "BeforeAgent" => "UserPromptSubmit",
            "PreCompress" => "PreCompact",
            _ => eventName,
        };
        Canonicalize(input);
        AliasStdout(input, "llmContent", nonEmpty: false);
    }

    private static void Devin(HookInput input, string cliEvent, string? projectDir)
    {
        Canonicalize(input);
        // A failed call reports an empty output: nothing to lift.
        AliasStdout(input, "output", nonEmpty: true);
        if (input.Cwd is null && !string.IsNullOrEmpty(projectDir))
        {
            input.Cwd = projectDir;
        }
        if (input.HookEventName.Length == 0)
        {
            input.HookEventName = cliEvent;
        }
        if (input.HookEventName is "PostCompaction")
        {
            input.HookEventName = "PostCompact";
        }
    }

    private static void CommandCode(HookInput input, string cliEvent, string? projectDir)
    {
        Canonicalize(input);
        if (input.Cwd is null && !string.IsNullOrEmpty(projectDir))
        {
            input.Cwd = projectDir;
        }
        if (input.HookEventName.Length == 0)
        {
            input.HookEventName = cliEvent;
        }
    }

    // Grok keeps only Claude's hook_event_name; its other keys are camelCase. Only its terminal
    // tool becomes Bash: a Read rewrite has not been checked against Grok's read_file schema.
    private static void Grok(HookInput input, string cliEvent)
    {
        var session = input.TakeText("sessionId");
        var name = input.TakeText("toolName");
        var toolInput = input.Take("toolInput");
        var result = input.Take("toolResult");
        var useId = input.TakeText("toolUseId");
        var mode = input.TakeText("permissionMode");
        var root = input.TakeText("workspaceRoot");
        if (input.SessionId.Length == 0)
        {
            input.SessionId = session ?? "";
        }
        if ((input.ToolName ?? name) is { } tool)
        {
            input.ToolName = tool is "run_terminal_command" ? "Bash" : tool;
        }
        input.ToolInput ??= toolInput;
        input.ToolResponse ??= result;
        input.ToolUseId ??= useId;
        input.PermissionMode ??= mode;
        input.Cwd ??= root;
        AliasStdout(input, "output_for_prompt", nonEmpty: false);
        if (input.HookEventName.Length == 0)
        {
            input.HookEventName = cliEvent;
        }
    }

    // Cline sends camelCase keys with the tool nested under tool_call or tool_result.
    private static void Cline(HookInput input, string cliEvent)
    {
        var hookName = input.TakeText("hookName");
        var taskId = input.TakeText("taskId");
        if (input.SessionId.Length == 0 && taskId is not null)
        {
            input.SessionId = taskId;
        }
        var roots = input.Take("workspaceRoots");
        if (
            input.Cwd is null
            && roots is JsonArray list
            && list.Select(r => r is JsonValue v && v.TryGetValue<string>(out var s) ? s : null)
                .FirstOrDefault(s => s is not null)
                is { } first
            && first.Trim() is { Length: > 0 } trimmed
        )
        {
            input.Cwd = trimmed;
        }
        if (input.ToolName is null && input.Take("tool_call") is JsonObject call)
        {
            ClineCall(input, call);
        }
        if (input.ToolResponse is null && input.Take("tool_result") is JsonObject result)
        {
            ClineResult(input, result);
        }
        var eventName =
            !string.IsNullOrEmpty(hookName) ? hookName
            : input.HookEventName.Length == 0 ? cliEvent
            : input.HookEventName;
        input.HookEventName = ClineEvent(eventName);
    }

    private static void ClineCall(HookInput input, JsonObject call)
    {
        var name = TakeText(call, "name");
        var toolInput = Take(call, "input");
        var useId = TakeText(call, "id");
        Spill(input, call);
        if (name is not null)
        {
            input.ToolName = ToolNames.Canonical(name);
            if (input.ToolName is "Bash")
            {
                SingleCommand(toolInput);
            }
            input.ToolInput = toolInput;
        }
        input.ToolUseId ??= useId;
    }

    private static void ClineResult(HookInput input, JsonObject result)
    {
        var name = TakeText(result, "name");
        var toolInput = Take(result, "input");
        var output = Take(result, "output");
        var error = Take(result, "error");
        var useId = TakeText(result, "id");
        Spill(input, result);
        if (input.ToolName is null && name is not null)
        {
            input.ToolName = ToolNames.Canonical(name);
        }
        if (input.ToolInput is null && toolInput is not null)
        {
            if (input.ToolName is "Bash")
            {
                SingleCommand(toolInput);
            }
            input.ToolInput = toolInput;
        }
        input.ToolUseId ??= useId;
        input.ToolResponse = (AsText(output), AsText(error)) switch
        {
            ({ } o, { Length: > 0 } e) => new JsonObject { ["stdout"] = o, ["stderr"] = e },
            _ when output is not null => output,
            _ => error ?? JsonValue.Create(""),
        };
    }

    private static string ClineEvent(string eventName) =>
        eventName switch
        {
            "tool_call" or "PreToolUse" => "PreToolUse",
            "tool_result" or "PostToolUse" => "PostToolUse",
            "agent_start" or "agent_resume" or "TaskStart" or "TaskResume" or "SessionStart" =>
                "SessionStart",
            "prompt_submit" or "UserPromptSubmit" => "UserPromptSubmit",
            "agent_end"
            or "session_shutdown"
            or "TaskComplete"
            or "SessionShutdown"
            or "SessionEnd" => "SessionEnd",
            // agent_error, agent_abort and unknown names reach no plugin.
            _ => "Noop",
        };

    private static string CursorEvent(string cliEvent, string stdinEvent) =>
        (stdinEvent.Length == 0 ? cliEvent : stdinEvent) switch
        {
            "sessionStart" => "SessionStart",
            "beforeSubmitPrompt" => "UserPromptSubmit",
            "sessionEnd" => "SessionEnd",
            "postToolUse" or "PostToolUse" or "afterShellExecution" => "PostToolUse",
            "preToolUse" or "PreToolUse" or "beforeShellExecution" => "PreToolUse",
            "afterMCPExecution" => "AfterMCPExecution",
            "preCompact" => "PreCompact",
            var other => other,
        };

    private static void Canonicalize(HookInput input)
    {
        if (input.ToolName is { } name)
        {
            input.ToolName = ToolNames.Canonical(name);
        }
    }

    // A tool response's own output key, aliased to the stdout the plugins read.
    private static void AliasStdout(HookInput input, string key, bool nonEmpty)
    {
        if (
            input.ToolResponse is JsonObject response
            && !response.ContainsKey("stdout")
            && AsText(response[key]) is { } text
            && (!nonEmpty || text.Length > 0)
        )
        {
            response["stdout"] = text;
        }
    }

    // Cursor's tool output is often the result object as a JSON string.
    private static JsonNode? TakeJsonish(HookInput input, string key)
    {
        var node = input.Take(key);
        return AsText(node) is { } text ? ParseOr(text, node) : node;
    }

    // Copilot documents toolArgs as a JSON string; only one that holds an object is decoded,
    // so anything else reaches the plugins as it came.
    private static JsonNode DecodeToolArgs(JsonNode args) =>
        AsText(args) is { } text && ParseOr(text, null) is JsonObject decoded ? decoded : args;

    private static JsonNode? ParseOr(string text, JsonNode? fallback)
    {
        try
        {
            return JsonNode.Parse(text) ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    // A one-entry commands list also gets the command the Bash plugins match on.
    private static void SingleCommand(JsonNode? input)
    {
        if (
            input is JsonObject map
            && !map.ContainsKey("command")
            && map["commands"] is JsonArray { Count: 1 } commands
        )
        {
            map["command"] = commands[0]?.DeepClone();
        }
    }

    private static void Spill(HookInput input, JsonObject rest)
    {
        foreach (var key in rest.Select(kv => kv.Key).ToList())
        {
            var value = Take(rest, key);
            input.Fields[key] = value;
        }
    }

    private static JsonNode? Take(JsonObject obj, string key)
    {
        if (!obj.TryGetPropertyValue(key, out var value))
        {
            return null;
        }
        obj.Remove(key);
        return value;
    }

    private static string? TakeText(JsonObject obj, string key) => AsText(Take(obj, key));

    private static string? AsText(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var s) ? s : null;
}
