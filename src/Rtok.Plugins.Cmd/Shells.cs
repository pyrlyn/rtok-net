using Rtok.Core;

namespace Rtok.Plugins.Cmd;

/// <summary>Shell resolution and quoting for <c>rtok run</c> and its Bash rewrite.</summary>
internal static class Shells
{
    // `$SHELL` is honoured only for these: `ScriptFor` emits `{ …\n} 2>&1`, which fish, nu
    // or xonsh cannot parse. An explicit `[plugins.cmd] shell` is trusted as is.
    private static readonly string[] PosixShells = ["sh", "bash", "zsh", "dash", "ksh", "ash"];

    /// <summary>POSIX single quotes; an embedded <c>'</c> becomes <c>'"'"'</c>.</summary>
    public static string ShQuote(string s) =>
        "'" + s.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";

    /// <summary>
    /// Quotes a command for the rewrite <c>rtok run -- &lt;quoted&gt;</c>. On Windows the
    /// rewrite often runs under PowerShell, where the POSIX embedding breaks on apostrophes;
    /// PowerShell single quotes (<c>''</c> escape) are safe there.
    /// </summary>
    public static string WrapQuote(string s, bool windows) =>
        windows ? "'" + s.Replace("'", "''", StringComparison.Ordinal) + "'" : ShQuote(s);

    /// <summary>Quotes one argv word for <c>cmd.exe /C</c>: double quotes, <c>"</c> doubled.</summary>
    public static string CmdQuote(string s)
    {
        if (s.Length == 0)
        {
            return "\"\"";
        }

        var plain = !s.Any(c =>
            char.IsWhiteSpace(c) || c is '"' or '^' or '&' or '|' or '<' or '>' or '%'
        );
        return plain ? s : "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    /// <summary>Classifies by the executable stem, so an explicit shell still gets the right flags.</summary>
    public static ShellKind Kind(string shell) =>
        Text.AsciiLower(Names.CmdStem(shell)) switch
        {
            "cmd" => ShellKind.Cmd,
            "powershell" or "pwsh" => ShellKind.PowerShell,
            _ => ShellKind.Posix,
        };

    /// <summary>
    /// Joins argv into a shell body. A single argument is already a snippet (the rewrite
    /// quotes the original command as one argv); several are a CLI argv list and are quoted
    /// so spaces stay inside one word. Every form merges stderr into stdout.
    /// </summary>
    public static string ScriptFor(ShellKind kind, IReadOnlyList<string> args)
    {
        string Inner(Func<string, string> quote) =>
            args.Count == 1 ? args[0] : string.Join(' ', args.Select(quote));
        return kind switch
        {
            ShellKind.Cmd => $"({Inner(CmdQuote)}) 2>&1",
            // `& { … }` runs a bare external; `*>&1` merges streams on PS 5+.
            ShellKind.PowerShell =>
                $"& {{ {Inner(a => "'" + a.Replace("'", "''", StringComparison.Ordinal) + "'")} }} *>&1",
            _ => $"{{ {Inner(ShQuote)}\n}} 2>&1",
        };
    }

    /// <summary>
    /// The shell binary: the config override, then a POSIX <c>$SHELL</c>, then the host
    /// default (<c>%ComSpec%</c> or <c>cmd.exe</c> on Windows, <c>/bin/sh</c> elsewhere).
    /// </summary>
    public static string Resolve(string configured, string? envShell, bool windows, string? comSpec)
    {
        if (configured.Length > 0)
        {
            return configured;
        }

        if (envShell is not null)
        {
            var stem = Names.CmdStem(envShell);
            if (
                Array.Exists(
                    PosixShells,
                    p => string.Equals(p, stem, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return envShell;
            }
        }

        if (windows)
        {
            return string.IsNullOrEmpty(comSpec) ? "cmd.exe" : comSpec;
        }

        return "/bin/sh";
    }

    /// <summary>
    /// Flags plus body for the shell process. POSIX gets <c>-c</c>, not <c>-lc</c>: the host
    /// already ran its login profile, and a second login shell costs time and may reorder PATH.
    /// </summary>
    public static string[] Args(string shell, string body) =>
        Kind(shell) switch
        {
            ShellKind.Cmd => ["/D", "/C", body],
            ShellKind.PowerShell => ["-NoProfile", "-NonInteractive", "-Command", body],
            _ => ["-c", body],
        };
}
