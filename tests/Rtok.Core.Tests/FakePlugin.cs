using Rtok.Plugin.Sdk;

namespace Rtok.Core.Tests;

/// <summary>A plugin whose every answer is a lambda the test sets.</summary>
internal sealed class FakePlugin(string id, bool defaultOn = true) : IPlugin
{
    public Func<PreToolUse, PreToolDecision?> PreTool { get; init; } = _ => null;

    public Func<PostToolUse, string?> PostTool { get; init; } = _ => null;

    public Func<SessionStart, Injection?> SessionStart { get; init; } = _ => null;

    public Action PreCompact { get; init; } = () => { };

    public Manifest Manifest { get; } = new(id, Surface.Hook, defaultOn);

    public DashboardPage DashboardPage { get; } = new(id, "fake", SavesTokens: false);

    public PreToolDecision? OnPreTool(PreToolUse ev, Ctx cx) => PreTool(ev);

    public string? OnPostTool(PostToolUse ev, Ctx cx) => PostTool(ev);

    public Injection? OnSessionStart(SessionStart ev, Ctx cx) => SessionStart(ev);

    public void OnPreCompact(PreCompact ev, Ctx cx) => PreCompact();
}
