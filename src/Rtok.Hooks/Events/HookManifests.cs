using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Rtok.Hook.Contracts;

namespace Rtok.Hooks.Events;

/// <summary>
/// The hook manifest each host's plugin ships, written from <see cref="HookEvents"/> (rtok's
/// were hand-written and only checked against the table). Each launcher prefers the
/// <c>rtok-hook</c> client or <c>rtok</c> on the PATH, then ketch's install, and exits 0 with no
/// output when rtok is missing; on SessionStart the Copilot and Cursor ones say so instead.
/// Cline and Command Code take a script per event, so they have no manifest here
/// (<see cref="ScriptEvents"/>).
/// </summary>
public static class HookManifests
{
    private const string Missing =
        "rtok is not installed; run ketch install pyrlyn/rtok to enable it.";

    private static readonly JsonWriterOptions Options = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The hosts with a manifest, and the plugin-relative path each one ships at.</summary>
    public static IReadOnlyList<(string Host, string Path)> Files { get; } =
    [
        ("claude", "hooks/hooks.json"),
        ("codebuddy", "hooks/hooks.json"),
        ("codex", "hooks/hooks.json"),
        ("copilot", "hooks/hooks.json"),
        ("cursor", "hooks/hooks.json"),
        ("devin", "hooks.json"),
        ("gemini", "hooks/hooks.json"),
        ("grok", "hooks/hooks.json"),
        ("kimi", "hooks.json"),
        ("qwen", "hooks/hooks.json"),
        ("zcode", "hooks/hooks.json"),
    ];

    /// <summary>The hosts that run one script per event, named by the host event.</summary>
    public static IReadOnlyList<string> ScriptHosts { get; } = ["cline", "commandcode"];

    /// <summary>The script names a script host links, one per host event.</summary>
    public static IReadOnlyList<string> ScriptEvents(string host) =>
        [.. HookEvents.ForHost(host).Select(r => r.HostEvent).Distinct(StringComparer.Ordinal)];

    /// <summary>The manifest of <paramref name="host"/>, with a final newline.</summary>
    /// <exception cref="ArgumentException">The host has no manifest.</exception>
    public static string For(string host)
    {
        var rows = HookEvents.ForHost(host);
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer, Options))
        {
            switch (host)
            {
                case "kimi":
                    // The hooks array of kimi.plugin.json; the plugin metadata sits beside it.
                    w.WriteStartArray();
                    foreach (var row in rows)
                    {
                        w.WriteStartObject();
                        w.WriteString("event", row.HostEvent);
                        Matcher(w, row);
                        w.WriteString("command", $"rtok hook {row.RtokEvent}");
                        w.WriteNumber("timeout", 5);
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                    break;
                case "copilot":
                case "cursor":
                    w.WriteStartObject();
                    w.WriteNumber("version", 1);
                    w.WritePropertyName("hooks");
                    Events(w, rows, row => Flat(w, host, row));
                    w.WriteEndObject();
                    break;
                case "devin":
                    Events(w, rows, row => Nested(w, host, row));
                    break;
                case "claude" or "codebuddy" or "codex" or "gemini" or "grok" or "qwen" or "zcode":
                    w.WriteStartObject();
                    w.WritePropertyName("hooks");
                    Events(
                        w,
                        rows,
                        row => Nested(w, host, row),
                        host is "claude" ? ClaudeWorktree : null
                    );
                    w.WriteEndObject();
                    break;
                default:
                    throw new ArgumentException($"{host} has no hook manifest", nameof(host));
            }
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }

    // One property per host event, its entries in table order.
    private static void Events(
        Utf8JsonWriter w,
        IReadOnlyList<HookEventRow> rows,
        Action<HookEventRow> entry,
        Action<Utf8JsonWriter>? tail = null
    )
    {
        w.WriteStartObject();
        foreach (var group in rows.GroupBy(r => r.HostEvent, StringComparer.Ordinal))
        {
            w.WriteStartArray(group.Key);
            foreach (var row in group)
            {
                entry(row);
            }
            w.WriteEndArray();
        }
        tail?.Invoke(w);
        w.WriteEndObject();
    }

    // Claude's shape: {matcher?, hooks: [{type, command, ...}]}.
    private static void Nested(Utf8JsonWriter w, string host, HookEventRow row)
    {
        w.WriteStartObject();
        Matcher(w, row);
        w.WriteStartArray("hooks");
        w.WriteStartObject();
        if (host is "zcode")
        {
            w.WriteString("type", "process");
            w.WriteString("command", "${ZCODE_PLUGIN_ROOT}/scripts/hook.sh");
            w.WriteStartArray("args");
            w.WriteStringValue(row.RtokEvent);
            w.WriteEndArray();
            w.WriteNumber("timeoutMs", 5000);
        }
        else
        {
            w.WriteString("type", "command");
            w.WriteString("command", Command(host, row.RtokEvent));
            if (host is "codex")
            {
                w.WriteString("commandWindows", $"rtok hook {row.RtokEvent} --host codex");
            }
            // Gemini counts its timeout in milliseconds.
            w.WriteNumber("timeout", host is "gemini" ? 5000 : 5);
        }
        w.WriteEndObject();
        w.WriteEndArray();
        w.WriteEndObject();
    }

    // Copilot's and Cursor's shape: the command is the entry.
    private static void Flat(Utf8JsonWriter w, string host, HookEventRow row)
    {
        var args = $"hook {row.RtokEvent} --host {host}";
        var start = row.RtokEvent is "SessionStart";
        w.WriteStartObject();
        if (host is "copilot")
        {
            var note = start ? $"{{\"additionalContext\":\"{Missing}\"}}" : null;
            w.WriteString("type", "command");
            w.WriteString("bash", HookLines.Posix(args, note));
            w.WriteString("powershell", HookLines.PowerShell(args, note));
            w.WriteNumber("timeoutSec", 5);
        }
        else
        {
            Matcher(w, row);
            // Braced: Cursor appends a heredoc to the command.
            var note = start ? $"{{\"additional_context\":\"{Missing}\"}}" : null;
            w.WriteString("command", $"{{ {HookLines.Posix(args, note)}; }}");
        }
        w.WriteEndObject();
    }

    private static string Command(string host, string rtokEvent) =>
        host switch
        {
            // Claude's plugin ships its own scripts/hook.sh, which also finds ketch's install.
            "claude" => $"command -v rtok-hook >/dev/null 2>&1 && exec rtok-hook {rtokEvent}; "
                + $"command -v rtok >/dev/null 2>&1 && exec rtok hook {rtokEvent}; "
                + $"exec \"${{CLAUDE_PLUGIN_ROOT}}/scripts/hook.sh\" {rtokEvent}",
            "codebuddy" =>
                $"command -v rtok-hook >/dev/null 2>&1 && exec rtok-hook {rtokEvent} --host codebuddy; "
                    + $"command -v rtok >/dev/null 2>&1 && exec rtok hook {rtokEvent} --host codebuddy; "
                    + $"exec \"${{CODEBUDDY_PLUGIN_ROOT}}/scripts/hook.sh\" {rtokEvent}",
            "gemini" => $"rtok hook {rtokEvent} --host gemini",
            // Qwen speaks Claude's envelope, so it needs no --host.
            "qwen" => $"rtok hook {rtokEvent}",
            "devin" => HookLines.Posix($"hook {rtokEvent} --host devin"),
            _ => Resolver(host, rtokEvent) + "exit 0",
        };

    private static string Resolver(string host, string rtokEvent) =>
        $"command -v rtok >/dev/null 2>&1 && exec rtok hook {rtokEvent} --host {host}; "
        + $"[ -x \"$HOME/.ketch/bin/rtok\" ] && exec \"$HOME/.ketch/bin/rtok\" hook {rtokEvent} --host {host}; ";

    private static void Matcher(Utf8JsonWriter w, HookEventRow row)
    {
        if (row.Matcher.Length > 0)
        {
            w.WriteString("matcher", row.Matcher);
        }
    }

    // D31: the worktree hooks run through the plugin's script, which falls back to the host's
    // own worktree when rtok cannot make one. They are exempt from the 10 ms rule.
    private static void ClaudeWorktree(Utf8JsonWriter w)
    {
        foreach (var name in new[] { "WorktreeCreate", "WorktreeRemove" })
        {
            w.WriteStartArray(name);
            w.WriteStartObject();
            w.WriteStartArray("hooks");
            w.WriteStartObject();
            w.WriteString("type", "command");
            w.WriteString("command", $"\"${{CLAUDE_PLUGIN_ROOT}}/scripts/worktree.sh\" {name}");
            w.WriteNumber("timeout", 120);
            w.WriteEndObject();
            w.WriteEndArray();
            w.WriteEndObject();
            w.WriteEndArray();
        }
    }
}
