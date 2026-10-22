using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rtok.Hooks.Hosts;
using Rtok.Testing;

namespace Rtok.Hooks.Tests.Hosts;

/// <summary>Each host's envelope in <c>tests/fixtures/hooks/hosts/&lt;host&gt;/&lt;event&gt;.json</c>:
/// the canonical input the adapter reads from it, and the replies the host's writer gives.</summary>
public sealed class HostEnvelopeTests
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static TheoryData<string, string> Envelopes()
    {
        var data = new TheoryData<string, string>();
        var root = RepoPaths.Fixture("hooks", "hosts");
        foreach (var dir in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
        {
            foreach (
                var file in Directory.EnumerateFiles(dir, "*.json").Order(StringComparer.Ordinal)
            )
            {
                data.Add(Path.GetFileName(dir), Path.GetFileNameWithoutExtension(file));
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Envelopes))]
    public Task The_envelope_reads_and_writes_in_the_hosts_shape(string host, string hookEvent)
    {
        var bytes = File.ReadAllBytes(
            RepoPaths.Fixture("hooks", "hosts", host, $"{hookEvent}.json")
        );
        var input = HookInput.Parse(bytes)!;
        var env = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DEVIN_PROJECT_DIR"] = "/home/dev/app",
        };
        if (host is "grok")
        {
            env["GROK_HOOK_EVENT"] = hookEvent;
        }

        var adapted = HostAdapters.Adapt(
            host,
            hookEvent,
            input,
            name => env.TryGetValue(name, out var value) ? value : null
        );

        var text = new StringBuilder()
            .AppendLine($"adapted: {adapted}")
            .AppendLine("input:")
            .AppendLine(JsonNode.Parse(input.ToJson())!.ToJsonString(Indented));
        foreach (var (label, reply) in Replies(input.HookEventName))
        {
            text.AppendLine($"{label}:")
                .AppendLine(Encoding.UTF8.GetString(HostWriters.Write(host, reply, input.Prompt)));
        }
        return Verify(text.ToString()).UseParameters(host, hookEvent);
    }

    [Theory]
    [MemberData(nameof(Envelopes))]
    public void The_empty_reply_is_the_no_op_for_every_host(string host, string hookEvent) =>
        Assert.Equal("{}"u8.ToArray(), HostWriters.Write(host, new HookReply(hookEvent)));

    private static IEnumerable<(string, HookReply)> Replies(string eventName) =>
        eventName is "PreToolUse"
            ?
            [
                ("deny", HookReply.Deny(eventName, "no shell here")),
                (
                    "rewrite",
                    new HookReply(eventName)
                    {
                        Reason = "rtok read",
                        UpdatedInput = new JsonObject { ["command"] = "rtok read README.md" },
                    }
                ),
            ]
            : [("context", new HookReply(eventName) { AdditionalContext = "rtok context" })];
}
