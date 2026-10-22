using Rtok.Agent.Sdk;
using Rtok.Hook.Contracts;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Hooks;

/// <summary>
/// rtok's hook entries on one app, in the host's own file and shape (rtok's per-host
/// <c>run(cfg, remove)</c>). Cline's hooks are links into rtok's plugin tree and come with the
/// plugin module (T35.5): until then its row reports state and writes nothing.
/// </summary>
internal sealed class HooksModule(AgentHost host, HostVariant variant, HostContext ctx)
{
    public const string Name = "hooks";

    /// <summary>How far this app supports hooks.</summary>
    public Support Support { get; } = host.Hooks.Of(variant.Kind);

    /// <summary>The files rtok's hooks live in on this app.</summary>
    public IReadOnlyList<OwnedFile> Files { get; } =
        host.Hooks.Of(variant.Kind).Level == SupportLevel.No ? [] : For(host, variant, ctx);

    /// <summary>Whether rtok has a hook in any of them.</summary>
    public bool Installed() => Files.Any(f => f.Installed(ctx.Disk));

    /// <summary>Installs, updates or removes the hooks; one report per file.</summary>
    /// <exception cref="IOException">A file exists but cannot be read.</exception>
    /// <exception cref="InvalidDataException">A file does not parse, or is not shaped as the host writes it.</exception>
    public IReadOnlyList<string> Apply(bool remove, Apply apply) =>
        !remove && Support.Level != SupportLevel.Yes
            ? []
            : [.. Files.Select(f => f.Apply(remove, apply, ctx.Disk))];

    private static IReadOnlyList<OwnedFile> For(AgentHost host, HostVariant v, HostContext ctx)
    {
        string Path(string fallback) =>
            ctx.PathOf(ctx.HookPaths, $"{host.Id}.{v.SurfaceName}", fallback);
        var windows = ctx.Env.Os == McpOs.Windows;
        OwnedFile? file = host.Id switch
        {
            "claude" => Nested(ctx, "claude", Path("~/.claude/settings.json")),
            "qwen" => Nested(ctx, "qwen", Path("~/.qwen/settings.json")),
            "codebuddy" => Nested(
                ctx,
                "codebuddy",
                Path(
                    v.Kind == HostKind.Desktop
                        ? "~/.workbuddy/settings.json"
                        : "~/.codebuddy/settings.json"
                ),
                "codebuddy"
            ),
            "codex" => Nested(ctx, "codex", Path("~/.codex/hooks.json"), "codex", bare: true),
            "devin" => Nested(
                ctx,
                "devin",
                Path(windows ? "$APPDATA/devin/config.json" : "~/.config/devin/config.json"),
                "devin"
            ),
            "zcode" => ZCode(ctx, Path("~/.zcode/cli/config.json")),
            "gemini" => Gemini(ctx, Path("~/.gemini/settings.json")),
            "cursor" => new CursorHooks(Path("~/.cursor/hooks.json"), ctx),
            "copilot" => new CopilotHooks(Path("~/.copilot/hooks/rtok.json"), ctx),
            "commandcode" => new CommandCodeHooks(Path("~/.commandcode/settings.json"), ctx),
            "kimi" => new KimiHooks(Path("~/.kimi-code/config.toml"), ctx),
            "codewhale" => new CodeWhaleHooks(Path("~/.codewhale/config.toml"), ctx),
            _ => null,
        };
        return file is null ? [] : [file];
    }

    /// <summary>
    /// Claude's shape and ownership, on every row of <paramref name="id"/>; <paramref name="flag"/>
    /// adds <c>--host</c>, and only the flagless form prints the SessionStart note.
    /// </summary>
    /// <param name="bare">Codex: the form before <c>--host</c> (rtok T645) is rtok's too, and rewritten.</param>
    private static NestedHooks Nested(
        HostContext ctx,
        string id,
        string path,
        string? flag = null,
        bool bare = false
    )
    {
        string Args(string ev) => flag is null ? $"hook {ev}" : $"hook {ev} --host {flag}";
        return new(path)
        {
            At = ["hooks"],
            Rows = HookEvents.ForHost(id),
            Timeout = ctx.Settings.HookTimeoutS,
            Command = ev =>
                HookText.Command(
                    ctx,
                    Args(ev),
                    flag is null && ev is "SessionStart" ? HookText.ClaudeNote : null
                ),
            Ours = (c, ev) =>
                HookText.Runs(ctx, c, Args(ev)) || bare && HookText.Runs(ctx, c, $"hook {ev}"),
        };
    }

    /// <summary>ZCode: under <c>hooks.events</c>, in milliseconds, switched on by <c>hooks.enabled</c>.</summary>
    private static NestedHooks ZCode(HostContext ctx, string path) =>
        new(path)
        {
            At = ["hooks", "events"],
            Rows = HookEvents.ForHost("zcode"),
            TimeoutKey = "timeoutMs",
            Timeout = ctx.Settings.HookTimeoutS * 1000,
            Enable = true,
            // The desktop app runs hooks without the user's PATH (rtok desktop_command).
            Command = ev => $"{ctx.Rtok.Self ?? "rtok"} hook {ev}",
            Ours = (c, ev) => HookText.Runs(ctx, c, $"hook {ev}"),
        };

    /// <summary>Gemini: its own event names, no matchers, milliseconds, nothing pruned.</summary>
    private static NestedHooks Gemini(HostContext ctx, string path) =>
        new(path)
        {
            At = ["hooks"],
            Rows = HookEvents.InstallerRows("gemini"),
            Timeout = ctx.Settings.HookTimeoutS * 1000,
            Prune = false,
            Bare = true,
            Command = ev => $"{HookText.Quote(ctx.Command)} hook {ev} --host gemini",
            Ours = (c, ev) => HookText.Runs(ctx, c, $"hook {ev} --host gemini", resolver: false),
        };
}
