using System.Text.Json;
using System.Text.Json.Nodes;
using Rtok.Hooks.Hosts;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any stdin through a host's adapter and writer, the first byte picking the host and the
/// event: the payload parses or is refused, an adapted one still serializes to a JSON object,
/// and every reply the host's writer gives for it is <c>{}</c> or one valid JSON value.
/// </summary>
internal static class HookIo
{
    private static readonly string[] Hosts =
    [
        "claude",
        "cline",
        "codebuddy",
        "codewhale",
        "codex",
        "commandcode",
        "copilot",
        "cursor",
        "devin",
        "gemini",
        "grok",
        "kimi",
        "qwen",
        "zcode",
    ];

    private static readonly string[] Events =
    [
        "PreToolUse",
        "PostToolUse",
        "SessionStart",
        "UserPromptSubmit",
        "SubagentStart",
        "PreCompact",
        "PostCompact",
        "SessionEnd",
    ];

    public static void Run(ReadOnlySpan<byte> data)
    {
        var pick = data.IsEmpty ? 0 : data[0];
        var host = Hosts[pick % Hosts.Length];
        var cliEvent = Events[pick / Hosts.Length % Events.Length];
        var stdin = data.IsEmpty ? data : data[1..];
        if (HookInput.Parse(stdin) is not { } input)
        {
            return;
        }
        HostAdapters.Adapt(host, cliEvent, input, _ => null);
        FuzzInput.Check(
            JsonNode.Parse(input.ToJson()) is JsonObject,
            "an adapted payload is not a JSON object"
        );
        var name = input.HookEventName;
        HookReply[] replies =
        [
            new(name),
            HookReply.Deny(name, "denied"),
            new(name) { Reason = "rewritten", UpdatedInput = input.ToolInput?.DeepClone() },
            new(name) { AdditionalContext = "context" },
        ];
        foreach (var reply in replies)
        {
            var bytes = HostWriters.Write(host, reply, input.Prompt);
            FuzzInput.Check(Valid(bytes), $"{host} wrote a reply that is not JSON");
            FuzzInput.Check(
                !reply.IsEmpty || bytes.AsSpan().SequenceEqual("{}"u8),
                $"{host} wrote an empty reply that is not {{}}"
            );
        }
    }

    private static bool Valid(byte[] bytes)
    {
        try
        {
            using var doc = JsonDocument.Parse(bytes);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
