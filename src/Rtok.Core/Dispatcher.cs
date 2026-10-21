using Rtok.Plugin.Sdk;

namespace Rtok.Core;

/// <summary>
/// Runs one hook event through every enabled plugin and merges what they answer: the first
/// deny wins, the last rewrite wins, PostToolUse context is joined under the inject budget,
/// and injections go in by priority up to it. Each plugin call is guarded on its own: a
/// plugin that throws loses only its own answer, and the throw is logged.
/// </summary>
public sealed class Dispatcher(PluginRegistry registry, ICapabilities host)
{
    /// <summary>PreToolUse: the first <see cref="PreToolDecision.Deny"/>, else the last
    /// <see cref="PreToolDecision.Rewrite"/>, else <see langword="null"/>.</summary>
    public PreToolDecision? PreTool(PreToolUse ev, string? agentId = null)
    {
        var cx = new Ctx(host, agentId);
        PreToolDecision? rewrite = null;
        foreach (var plugin in registry.Enabled())
        {
            switch (Guard(plugin, "PreToolUse", () => plugin.OnPreTool(ev, cx)))
            {
                case PreToolDecision.Deny deny:
                    return deny;
                case PreToolDecision.Rewrite r:
                    rewrite = r;
                    break;
            }
        }

        return rewrite;
    }

    /// <summary>PostToolUse: every plugin's context, one per line, fitted to the inject
    /// budget (<see cref="InjectBudget.Cap"/>); empty when none has any.</summary>
    public string PostTool(PostToolUse ev, string? agentId = null)
    {
        var cx = new Ctx(host, agentId);
        var parts = new List<string>();
        foreach (var plugin in registry.Enabled())
        {
            if (Guard(plugin, "PostToolUse", () => plugin.OnPostTool(ev, cx)) is { } part)
            {
                parts.Add(part);
            }
        }

        return InjectBudget.Cap(host, string.Join('\n', parts), InjectBudget.Tokens(host));
    }

    /// <summary>SessionStart (and the re-inject after a compaction, as source
    /// <c>compact</c>): the budgeted injections.</summary>
    public string SessionStart(SessionStart ev) =>
        Inject("SessionStart", (plugin, cx) => plugin.OnSessionStart(ev, cx));

    /// <summary>UserPromptSubmit: the budgeted injections.</summary>
    public string PromptSubmit(PromptSubmit ev) =>
        Inject("UserPromptSubmit", (plugin, cx) => plugin.OnPromptSubmit(ev, cx));

    /// <summary>SubagentStart: the budgeted injections, from the parent's context.</summary>
    public string SubagentStart(SubagentStart ev) =>
        Inject("SubagentStart", (plugin, cx) => plugin.OnSubagentStart(ev, cx));

    /// <summary>PreCompact: every plugin gets to save what it needs; nothing is returned.</summary>
    public void PreCompact(PreCompact ev)
    {
        var cx = new Ctx(host);
        foreach (var plugin in registry.Enabled())
        {
            Guard(
                plugin,
                "PreCompact",
                () =>
                {
                    plugin.OnPreCompact(ev, cx);
                    return true;
                }
            );
        }
    }

    // No offerings, no Measurement: UserPromptSubmit usually has none.
    private string Inject(string eventName, Func<IPlugin, Ctx, Injection?> offer)
    {
        var cx = new Ctx(host);
        var offered = new List<Injection>();
        foreach (var plugin in registry.Enabled())
        {
            if (Guard(plugin, eventName, () => offer(plugin, cx)) is { } injection)
            {
                offered.Add(injection);
            }
        }

        return offered.Count == 0
            ? ""
            : InjectBudget.Apply(host, offered, InjectBudget.Tokens(host));
    }

    private T? Guard<T>(IPlugin plugin, string eventName, Func<T> call)
    {
        try
        {
            return call();
        }
#pragma warning disable CA1031 // Fail open: one plugin's throw must not cost the others.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            host.Log("error", "plugin", plugin.Manifest.Id, $"{eventName} threw: {ex.Message}");
            return default;
        }
    }
}
