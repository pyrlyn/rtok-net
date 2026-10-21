using System.IO.Abstractions;
using Rtok.Config;
using Rtok.Hook.Contracts;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Cmd;

/// <summary>
/// The cmd plugin: shell output through <c>rtok run</c> and <c>rtok filter</c>, the rule
/// engine and the PreToolUse(Bash) rewrite that routes commands through <c>rtok run</c>.
/// </summary>
public sealed class CmdPlugin : IPlugin
{
    /// <inheritdoc/>
    public Manifest Manifest { get; } = new("cmd", Surface.Hook | Surface.Cli, DefaultOn: true);

    /// <inheritdoc/>
    public DashboardPage DashboardPage { get; } =
        new(
            "Bash / cmd",
            "Archive and filter command output; expand the original by id.",
            SavesTokens: true
        );

    /// <inheritdoc/>
    public PreToolDecision? OnPreTool(PreToolUse ev, Ctx cx)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(cx);
        return BashRewrite.PreTool(ev, cx.AgentId, Section(cx), OperatingSystem.IsWindows());
    }

    /// <summary>The <c>[plugins.cmd]</c> section, defaults when the host has none.</summary>
    internal static CmdSection Section(Ctx cx) =>
        cx.PluginConfig("cmd", CmdJson.Default.CmdSection);

    /// <summary>
    /// Rules from <paramref name="section"/>: built-ins, then the user file, then the drop-ins,
    /// with <c>~</c> expanded against <paramref name="env"/>.
    /// </summary>
    internal static CmdSettings Settings(
        IFileSystem fs,
        CmdSection section,
        Func<string, string?> env
    )
    {
        var home = RtokHome.Resolve(env);
        var user = RtokHome.UserHome(env);
        return CmdSettings.Load(
            fs,
            RtokHome.Expand(section.Rules, home, user),
            RtokHome.Expand(section.RulesDir, home, user),
            section.FailTailLines
        );
    }
}
