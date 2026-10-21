using System.Text.Json;
using Rtok.Plugin.Sdk;
using Rtok.Plugin.Sdk.Testing;
using Rtok.Plugins.Cmd;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any Bash string through the PreToolUse(Bash) rewrite: it comes back unchanged, or as its own
/// cd hops and <c>rtok run -- </c> with the rest quoted once. A heredoc, a trailing <c>&amp;</c>
/// or a <c>sudo</c> / <c>rtok</c> stem is never wrapped.
/// </summary>
internal static class BashRewriteText
{
    private const string Wrap = "rtok run -- ";

    private static readonly CmdPlugin Plugin = new();

    public static void Run(ReadOnlySpan<byte> data)
    {
        var command = FuzzInput.Text(data);
        var decision = Plugin.OnPreTool(Event(command), new Ctx(new MemoryHost()));
        if (decision is not PreToolDecision.Rewrite rewrite)
        {
            FuzzInput.Check(decision is null, "the rewrite answered with something else");
            return;
        }

        var wrapped = rewrite.Input["command"]?.GetValue<string>() ?? "";
        var at = wrapped.IndexOf(Wrap, StringComparison.Ordinal);
        FuzzInput.Check(at >= 0, "a rewrite without rtok run");
        FuzzInput.Check(
            command.StartsWith(wrapped[..at], StringComparison.Ordinal),
            "the rewrite changed what runs before rtok run"
        );
        var rest = command[at..].TrimStart();
        FuzzInput.Check(!command.Contains("<<", StringComparison.Ordinal), "a heredoc was wrapped");
        var end = command.TrimEnd();
        FuzzInput.Check(
            !end.EndsWith('&') || end.EndsWith("&&", StringComparison.Ordinal),
            "a background job was wrapped"
        );
        var stem = rest.Split(' ', 2)[0];
        FuzzInput.Check(
            !stem.Equals("sudo", StringComparison.OrdinalIgnoreCase)
                && !stem.Equals("rtok", StringComparison.OrdinalIgnoreCase),
            "a never-wrap stem was wrapped"
        );
    }

    private static PreToolUse Event(string command)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("command", command);
            json.WriteEndObject();
        }
        using var doc = JsonDocument.Parse(buffer.ToArray());
        return new PreToolUse("Bash", doc.RootElement.Clone());
    }
}
