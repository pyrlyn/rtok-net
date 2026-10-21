using System.Text.Json;
using System.Text.Json.Nodes;
using Rtok.Config;
using Rtok.Core;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Cmd;

/// <summary>PreToolUse(Bash): wraps the command in <c>rtok run --</c> unless a skip rule fires.</summary>
internal static class BashRewrite
{
    private static readonly HashSet<string> StateBuiltins = new(StringComparer.Ordinal)
    {
        "cd",
        "pushd",
        "popd",
        "export",
        "source",
        ".",
        "unset",
        "alias",
        "unalias",
        "set",
        "shopt",
        "umask",
        "trap",
        "declare",
        "typeset",
        "readonly",
    };

    /// <summary>The rewrite decision for <paramref name="ev"/>, or null to leave it alone.</summary>
    public static PreToolDecision? PreTool(
        PreToolUse ev,
        string? agentId,
        CmdSection cfg,
        bool windows
    )
    {
        if (!string.Equals(ev.ToolName, "Bash", StringComparison.Ordinal) || !cfg.Rewrite)
        {
            return null;
        }

        if (
            ev.ToolInput.ValueKind != JsonValueKind.Object
            || !ev.ToolInput.TryGetProperty("command", out var command)
            || command.ValueKind != JsonValueKind.String
        )
        {
            return null;
        }

        var full = command.GetString()!;
        if (SkipWrap(windows, full, cfg.NeverWrap, cfg.InteractiveStems))
        {
            return null;
        }

        // Sub-agents run behind a guard that must prove every git command stays inside their
        // worktree; it cannot see through the wrapper and would refuse the command.
        if (agentId is not null && RunsGit(full))
        {
            return null;
        }

        // The host keeps its shell's cwd between calls, so leading `cd <dir> &&` hops stay in
        // that shell and only the rest runs inside `rtok run`'s child.
        var cmd = full;
        while (ShellHops.StripCdHop(cmd, out _, out var rest))
        {
            cmd = rest;
        }

        // The command behind the hops needs the same never-wrap and interactive checks.
        if (
            cmd.Length != full.Length
            && SkipWrap(windows, cmd, cfg.NeverWrap, cfg.InteractiveStems)
        )
        {
            return null;
        }

        if (ChangesShellState(cmd))
        {
            return null;
        }

        if (JsonNode.Parse(ev.ToolInput.GetRawText()) is not JsonObject input)
        {
            return null;
        }

        // One argv so the outer shell cannot split on `&&`, `|`, `;` or redirects.
        input["command"] =
            $"{full[..(full.Length - cmd.Length)]}rtok run {AgentFlag(agentId)}-- {Shells.WrapQuote(cmd, windows)}";
        return new PreToolDecision.Rewrite(input, "wrapped by rtok");
    }

    /// <summary>
    /// True when the command must run unwrapped: a never-wrap stem, a heredoc, a background
    /// <c>&amp;</c>, an interactive flag, or (on Windows) an apostrophe, since the PowerShell
    /// <c>''</c> quoting would lose it if the rewrite reached a POSIX shell.
    /// </summary>
    public static bool SkipWrap(
        bool windows,
        string cmd,
        IList<string> neverWrap,
        IList<string> interactiveStems
    )
    {
        var tokens = Text.Words(cmd);
        var stem = Names.CmdStem(tokens.Length > 0 ? tokens[0] : "");
        if (neverWrap.Any(w => string.Equals(w, stem, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (
            cmd.Contains("<<", StringComparison.Ordinal)
            || (windows && cmd.Contains('\'', StringComparison.Ordinal))
            || tokens.Contains("&", StringComparer.Ordinal)
            || tokens.Contains("--interactive", StringComparer.Ordinal)
        )
        {
            return true;
        }

        if (
            tokens.Contains("-i", StringComparer.Ordinal)
            && InteractiveI(stem, tokens.AsSpan(1), interactiveStems)
        )
        {
            return true;
        }

        // A trailing `&` backgrounds the command (also `sleep 10&`); `&&` does not.
        var t = cmd.TrimEnd();
        return t.EndsWith('&') && !t.EndsWith("&&", StringComparison.Ordinal);
    }

    /// <summary>Only bare <c>[A-Za-z0-9_-]</c>, 1–64 bytes; anything else is dropped, not embedded.</summary>
    public static bool IsValidAgentId(string id) =>
        id.Length is > 0 and <= 64 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    /// <summary>
    /// True when <c>git</c> is a word anywhere in the raw text, as the host guard reads it.
    /// A word is a run of letters, digits and <c>_</c>: <c>git.exe</c> and <c>git-lfs</c>
    /// match, <c>github</c> does not.
    /// </summary>
    public static bool RunsGit(string cmd)
    {
        var start = 0;
        for (var i = 0; i <= cmd.Length; i++)
        {
            if (i < cmd.Length && (char.IsLetterOrDigit(cmd[i]) || cmd[i] == '_'))
            {
                continue;
            }

            if (cmd.AsSpan(start, i - start).Equals("git", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            start = i + 1;
        }

        return false;
    }

    /// <summary>
    /// A builtin whose effect outlives the command (cwd, environment, aliases) would be lost
    /// in <c>rtok run</c>'s child shell, so such a command is never wrapped.
    /// </summary>
    public static bool ChangesShellState(string cmd) =>
        Bounded
            .Stages(cmd)
            .Exists(stage =>
                (stage.Count > 0 && StateBuiltins.Contains(stage[0])) || IsBareAssignment(stage)
            );

    // `A=1` (or `A=1 B=2`) with no command word sets a variable in the calling shell; with
    // one (`A=1 cargo test`) the assignment is scoped to that command and wraps fine.
    private static bool IsBareAssignment(List<string> stage) =>
        stage.Count > 0
        && stage.TrueForAll(w =>
            w.IndexOf('=', StringComparison.Ordinal) is var eq and >= 0
            && eq > 0
            && (char.IsAsciiLetter(w[0]) || w[0] == '_')
            && w.AsSpan(0, eq).IndexOfAnyExcept(Identifier) < 0
        );

    private static readonly System.Buffers.SearchValues<char> Identifier =
        System.Buffers.SearchValues.Create(
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_"
        );

    // `-i` skips the wrap only for REPL and TTY stems: docker needs run/exec, kubectl exec.
    private static bool InteractiveI(
        string stem,
        ReadOnlySpan<string> args,
        IList<string> interactiveStems
    )
    {
        if (!interactiveStems.Any(s => string.Equals(s, stem, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        foreach (var t in args)
        {
            var hit = Text.AsciiLower(stem) switch
            {
                "docker" => t.Equals("run", StringComparison.OrdinalIgnoreCase)
                    || t.Equals("exec", StringComparison.OrdinalIgnoreCase),
                "kubectl" => t.Equals("exec", StringComparison.OrdinalIgnoreCase),
                _ => true,
            };
            if (hit)
            {
                return true;
            }
        }

        return Text.AsciiLower(stem) is not ("docker" or "kubectl");
    }

    private static string AgentFlag(string? agentId) =>
        agentId is not null && IsValidAgentId(agentId) ? $"--agent {agentId} " : "";
}
