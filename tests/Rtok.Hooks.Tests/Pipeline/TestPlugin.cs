using System.Text.Json.Nodes;
using Rtok.Plugin.Sdk;

namespace Rtok.Hooks.Tests.Pipeline;

/// <summary>Denies Bash, rewrites Read, answers every other event with a context line, and
/// records one measurement per tool call it sees. It counts its calls.</summary>
internal sealed class TestPlugin : IPlugin
{
    public int Calls { get; private set; }

    public bool Inject { get; init; } = true;

    public Manifest Manifest { get; } = new("test", Surface.Hook, DefaultOn: true);

    public DashboardPage DashboardPage { get; } = new("test", "test", SavesTokens: false);

    public PreToolDecision? OnPreTool(PreToolUse ev, Ctx cx)
    {
        Calls++;
        Measure(cx);
        return ev.ToolName switch
        {
            "Bash" => new PreToolDecision.Deny("no shell here"),
            "Read" => new PreToolDecision.Rewrite(new JsonObject { ["file_path"] = "/x" }, "moved"),
            _ => null,
        };
    }

    public string? OnPostTool(PostToolUse ev, Ctx cx)
    {
        Calls++;
        Measure(cx);
        return Inject ? "post context" : null;
    }

    public Injection? OnSessionStart(SessionStart ev, Ctx cx) => Injected($"start {ev.Source}");

    public Injection? OnPromptSubmit(PromptSubmit ev, Ctx cx) => Injected("prompt context");

    public Injection? OnSubagentStart(SubagentStart ev, Ctx cx) => Injected("subagent context");

    public void OnPreCompact(PreCompact ev, Ctx cx) => Calls++;

    private Injection? Injected(string text)
    {
        Calls++;
        return Inject ? new Injection("test", text, 0) : null;
    }

    private static void Measure(Ctx cx) =>
        cx.Host.Record(
            new Measurement
            {
                Plugin = "test",
                Kind = "probe",
                BeforeBytes = 10,
                AfterBytes = 4,
                EstBefore = 3,
                EstAfter = 1,
            }
        );
}
