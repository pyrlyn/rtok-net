using System.Text.Json;

namespace Rtok.Hooks.Hosts;

/// <summary>
/// A <see cref="HookReply"/> in the shape each host reads (rtok <c>*_output</c>). Claude's
/// <c>hookSpecificOutput</c> is the default: Devin, Grok, Command Code, Qwen, Kimi and ZCode
/// read it as Claude writes it. The empty reply is <c>{}</c> for every host. The flat shapes
/// keep rtok's key order, which was alphabetical.
/// </summary>
public static class HostWriters
{
    /// <summary>The bytes <paramref name="host"/> reads for <paramref name="reply"/>.</summary>
    /// <param name="host">The host id.</param>
    /// <param name="reply">The reply.</param>
    /// <param name="prompt">The prompt the reply answers, for hosts that replace it whole.</param>
    public static byte[] Write(string host, HookReply reply, string? prompt = null)
    {
        ArgumentNullException.ThrowIfNull(reply);
        if (reply.IsEmpty)
        {
            return "{}"u8.ToArray();
        }
        return host switch
        {
            "codex" => Claude(
                // A Codex rewrite takes effect only next to an allow; it reads no other key rtok
                // writes differently.
                reply.UpdatedInput
                    is not null
                && reply.Decision is null
                    ? reply with
                    {
                        Decision = "allow",
                    }
                    : reply
            ),
            "codebuddy" => CodeBuddy(reply),
            "copilot" => Copilot(reply),
            "cursor" => Cursor(reply),
            "gemini" => Gemini(reply),
            "cline" => Cline(reply),
            "codewhale" => CodeWhale(reply, prompt),
            _ => Claude(reply),
        };
    }

    private static byte[] Claude(HookReply reply) =>
        HookJson.Write(w =>
        {
            w.WriteStartObject();
            w.WriteStartObject("hookSpecificOutput");
            w.WriteString("hookEventName", reply.EventName);
            Text(w, "permissionDecision", reply.Decision);
            Text(w, "permissionDecisionReason", reply.Reason);
            Node(w, "updatedInput", reply);
            Text(w, "additionalContext", reply.AdditionalContext);
            w.WriteEndObject();
            w.WriteEndObject();
        });

    // Claude's shape, except that a rewrite travels as modifiedInput.
    private static byte[] CodeBuddy(HookReply reply) =>
        HookJson.Write(w =>
        {
            w.WriteStartObject();
            w.WriteStartObject("hookSpecificOutput");
            Text(w, "additionalContext", reply.AdditionalContext);
            w.WriteString("hookEventName", reply.EventName);
            Node(w, "modifiedInput", reply);
            Text(w, "permissionDecision", reply.Decision);
            Text(w, "permissionDecisionReason", reply.Reason);
            w.WriteEndObject();
            w.WriteEndObject();
        });

    // Flat: nothing nests under hookSpecificOutput.
    private static byte[] Copilot(HookReply reply) =>
        HookJson.Write(w =>
        {
            w.WriteStartObject();
            Text(w, "additionalContext", reply.AdditionalContext);
            Node(w, "modifiedArgs", reply);
            Text(w, "permissionDecision", reply.Decision);
            Text(w, "permissionDecisionReason", reply.Reason);
            w.WriteEndObject();
        });

    // Flat context on sessionStart and postToolUse; a deny or a shell rewrite stays nested.
    private static byte[] Cursor(HookReply reply) =>
        reply.AdditionalContext is { } context
            ? HookJson.Write(w =>
            {
                w.WriteStartObject();
                w.WriteString("additional_context", context);
                w.WriteEndObject();
            })
            : Claude(reply);

    // A top-level decision blocks; a rewrite is hookSpecificOutput.tool_input.
    private static byte[] Gemini(HookReply reply)
    {
        if (reply.EventName is "PreToolUse")
        {
            if (reply.Decision is "deny")
            {
                return HookJson.Write(w =>
                {
                    w.WriteStartObject();
                    w.WriteString("decision", "deny");
                    Text(w, "reason", reply.Reason);
                    w.WriteEndObject();
                });
            }
            return reply.UpdatedInput is null
                ? "{}"u8.ToArray()
                : HookJson.Write(w =>
                {
                    w.WriteStartObject();
                    w.WriteStartObject("hookSpecificOutput");
                    Node(w, "tool_input", reply);
                    w.WriteEndObject();
                    w.WriteEndObject();
                });
        }
        return reply.AdditionalContext is not { } context
            ? "{}"u8.ToArray()
            : HookJson.Write(w =>
            {
                w.WriteStartObject();
                w.WriteStartObject("hookSpecificOutput");
                w.WriteString("additionalContext", context);
                w.WriteEndObject();
                w.WriteEndObject();
            });
    }

    // overrideInput replaces one command, context goes to the next turn, cancel blocks.
    private static byte[] Cline(HookReply reply)
    {
        var deny = reply.Decision is "deny";
        var command =
            reply.UpdatedInput?["command"] is System.Text.Json.Nodes.JsonValue v
            && v.TryGetValue<string>(out var c)
                ? c
                : null;
        if (!deny && command is null && reply.AdditionalContext is null)
        {
            return "{}"u8.ToArray();
        }
        return HookJson.Write(w =>
        {
            w.WriteStartObject();
            if (deny)
            {
                w.WriteBoolean("cancel", true);
            }
            Text(w, "context", reply.AdditionalContext);
            if (deny)
            {
                Text(w, "errorMessage", reply.Reason);
            }
            if (command is not null)
            {
                w.WriteStartObject("overrideInput");
                w.WriteStartArray("commands");
                w.WriteStringValue(command);
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndObject();
        });
    }

    // CodeWhale replaces the prompt whole: the context is folded into a new one.
    private static byte[] CodeWhale(HookReply reply, string? prompt)
    {
        if (reply.AdditionalContext is not { } context)
        {
            return "{}"u8.ToArray();
        }
        var text = string.IsNullOrEmpty(prompt) ? context : $"{prompt}\n\n[hook context] {context}";
        return HookJson.Write(w =>
        {
            w.WriteStartObject();
            w.WriteString("text", text);
            w.WriteEndObject();
        });
    }

    private static void Text(Utf8JsonWriter w, string name, string? value)
    {
        if (value is not null)
        {
            w.WriteString(name, value);
        }
    }

    private static void Node(Utf8JsonWriter w, string name, HookReply reply)
    {
        if (reply.UpdatedInput is { } input)
        {
            w.WritePropertyName(name);
            input.WriteTo(w);
        }
    }
}
