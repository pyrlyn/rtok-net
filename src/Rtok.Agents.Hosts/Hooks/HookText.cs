using Rtok.Agent.Sdk;
using Rtok.Hook.Contracts;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Hooks;

/// <summary>The spelling every hooks writer shares: the binary token, the ownership tests, the report lines (rtok <c>agents::mod</c>).</summary>
internal static class HookText
{
    /// <summary>What a Claude-shaped SessionStart prints when rtok is nowhere.</summary>
    public const string ClaudeNote =
        "{\"hookSpecificOutput\":{\"hookEventName\":\"SessionStart\",\"additionalContext\":\"rtok is not installed; run ketch install pyrlyn/rtok to enable it.\"}}";

    /// <summary>
    /// <paramref name="bin"/> as a hook command line token: wrapped in <c>"…"</c> when it holds
    /// whitespace or a quote, an embedded quote doubled as cmd.exe wants it.
    /// </summary>
    public static string Quote(string bin) =>
        bin.Any(c => char.IsWhiteSpace(c) || c == '"')
            ? $"\"{bin.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : bin;

    /// <summary><paramref name="bin"/> without one layer of surrounding quotes.</summary>
    public static string Unquote(string bin)
    {
        var b = bin.Trim();
        return b.Length >= 2 && (b[0], b[^1]) is ('"', '"') or ('\'', '\'') ? b[1..^1] : b;
    }

    /// <summary>
    /// <c>&lt;bin&gt; hook &lt;args&gt;</c> on Windows or for an absolute bin; otherwise the line
    /// that finds rtok when the hook runs (<see cref="HookLines.Posix"/>).
    /// </summary>
    public static string Command(HostContext ctx, string args, string? note = null)
    {
        var bin = Quote(ctx.Command);
        return ctx.Env.Os == McpOs.Windows || bin is not "rtok"
            ? $"{bin} {args}"
            : HookLines.Posix(args, note);
    }

    /// <summary>
    /// Whether <paramref name="command"/> is rtok's line for <paramref name="args"/>: exactly
    /// <c>&lt;rtok bin&gt; &lt;args&gt;</c>, or the resolver, whose <c>exec rtok &lt;args&gt;;</c> only
    /// rtok writes. A user's chain ending in <c>rtok hook …</c> stays theirs.
    /// </summary>
    public static bool Runs(HostContext ctx, string command, string args, bool resolver = true)
    {
        if (command.EndsWith(" " + args, StringComparison.Ordinal))
        {
            return ctx.Rtok.Is(Unquote(command[..^(args.Length + 1)]));
        }
        return resolver && command.Contains($"exec rtok {args};", StringComparison.Ordinal);
    }

    /// <summary><c> &lt;matcher&gt;</c> for a report line; nothing for none.</summary>
    public static string Show(string matcher) => matcher.Length == 0 ? "" : " " + matcher;

    /// <summary>The lines, then <c>N additions, N updates, N removals</c> for the kinds there are; <see cref="Apply.NoChanges"/> for none.</summary>
    public static string Counted(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return Apply.NoChanges;
        }
        int Count(char mark) => lines.Count(l => l.Length > 0 && l[0] == mark);
        var counts = new[] { ('+', "additions"), ('~', "updates"), ('-', "removals") }
            .Where(k => Count(k.Item1) > 0)
            .Select(k => $"{Count(k.Item1)} {k.Item2}");
        return string.Join('\n', lines) + "\n" + string.Join(", ", counts);
    }

    /// <summary>A removal's report: the kept lines, then <c>{n} removed</c> when any went.</summary>
    public static string Removed(List<string> kept, int removed)
    {
        if (removed > 0)
        {
            kept.Add($"{removed} removed");
        }
        return kept.Count == 0 ? Apply.NoChanges : string.Join('\n', kept);
    }

    /// <summary>Whether a removal takes an rtok hook: an unchanged one goes, an edited one as <see cref="Ownership.KeepEdited"/> decides.</summary>
    public static bool Takes(Apply apply, bool unchanged, string at, List<string> kept)
    {
        if (unchanged)
        {
            return true;
        }
        if (Ownership.KeepEdited(apply, at) is { } line)
        {
            kept.Add(line);
            return false;
        }
        return true;
    }
}
