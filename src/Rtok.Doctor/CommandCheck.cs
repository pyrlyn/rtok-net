using System.Text;
using Pyrlyn.Shlex;
using Rtok.Mcp.Install;

namespace Rtok.Doctor;

/// <summary>
/// Judges a hook command without running it: shell syntax that only a run could judge is
/// unverified; otherwise the words are split, variables expanded, an interpreter's script or a
/// launcher's target taken, and that file looked up on disk or on <c>PATH</c> (rtok T331).
/// Windows follows cmd's quoting, <c>PATHEXT</c> and the cmd and PowerShell launch rules.
/// </summary>
internal sealed class CommandCheck(DoctorHome home)
{
    private static readonly HashSet<string> Interpreters = new(StringComparer.Ordinal)
    {
        "bash",
        "sh",
        "zsh",
        "node",
        "python",
        "python3",
        "deno",
        "bun",
        "ruby",
        "pwsh",
        "tsx",
    };

    private static readonly HashSet<string> Inline = new(StringComparer.Ordinal)
    {
        "-c",
        "-e",
        "--eval",
        "-p",
        "--print",
    };

    private static readonly HashSet<string> Builtins = new(StringComparer.Ordinal)
    {
        ":",
        ".",
        "[",
        "cd",
        "command",
        "echo",
        "exit",
        "export",
        "false",
        "printf",
        "pwd",
        "read",
        "set",
        "source",
        "test",
        "true",
        "type",
        "unset",
    };

    private static readonly HashSet<string> CmdBuiltins = new(StringComparer.OrdinalIgnoreCase)
    {
        "call",
        "copy",
        "del",
        "dir",
        "md",
        "mkdir",
        "move",
        "rd",
        "ren",
        "rem",
        "start",
        "title",
        "ver",
    };

    private const UnixFileMode Exec =
        UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    public Check Classify(string command, HookSource source)
    {
        var text = command.Trim();
        if (text.Length == 0)
        {
            return new(Verdict.Unverified, "empty command");
        }
        if (ShellSyntax(text) is { } why)
        {
            return new(Verdict.Unverified, $"{why}: cannot be checked without running it");
        }
        if (Words(text) is not { } words)
        {
            return new(Verdict.Unverified, "unbalanced quotes");
        }
        if (!home.Windows)
        {
            words = [.. words.SkipWhile(IsAssignment)];
            if (words.Count == 0)
            {
                return new(Verdict.Unverified, "only variable assignments");
            }
        }
        var expanded = new List<string>(words.Count);
        foreach (var word in words)
        {
            var (value, error) = Expand(word, source);
            if (error is not null)
            {
                return new(Verdict.Unverified, error);
            }
            expanded.Add(value);
        }
        var (target, direct, refusal) = Target(expanded);
        if (refusal is not null)
        {
            return new(Verdict.Unverified, refusal);
        }
        return home.Windows ? OnWindows(target, direct, source) : OnPosix(target, direct, source);
    }

    /// <summary>
    /// The command as duplicates compare it: words expanded (an unknown variable stays as
    /// written), a <c>./</c> path joined to the project for Claude, one space between words; text
    /// with shell syntax only has its whitespace collapsed.
    /// </summary>
    public string Normalize(string command, HookSource source)
    {
        var text = command.Trim();
        if (ShellSyntax(text) is not null || Words(text) is not { } words)
        {
            return string.Join(
                ' ',
                text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            );
        }
        return string.Join(
            ' ',
            words.Select(w =>
            {
                var (value, error) = Expand(w, source);
                var word = error is null ? value : w;
                var dotted =
                    word.StartsWith("./", StringComparison.Ordinal)
                    || (home.Windows && word.StartsWith(".\\", StringComparison.Ordinal));
                return source.RelativeOk && dotted ? Join(home.Cwd, word[2..]) : word;
            })
        );
    }

    private static readonly System.Buffers.SearchValues<char> Operators =
        System.Buffers.SearchValues.Create("|&;<>()\n");

    /// <summary>Why the text cannot be judged without a shell; null when it can.</summary>
    internal string? ShellSyntax(string text)
    {
        var bare = home.Windows ? OutsideQuotes(text) : text;
        if (
            bare.Contains("$(", StringComparison.Ordinal)
            || bare.Contains('`', StringComparison.Ordinal)
        )
        {
            return "command substitution";
        }
        if (bare.AsSpan().IndexOfAny(Operators) >= 0)
        {
            return "shell operators";
        }
        var first = bare.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return string.Equals(first, "eval", StringComparison.Ordinal) ? "eval" : null;
    }

    /// <summary>The words; null when a quote is left open.</summary>
    internal IReadOnlyList<string>? Words(string text) =>
        home.Windows ? WindowsWords(text)
        : ShellWords.TrySplit(text, out var words) ? words
        : null;

    /// <summary>cmd's splitting: <c>"</c> groups, <c>'</c> is literal, backslashes double only before a quote.</summary>
    internal static List<string>? WindowsWords(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        var quoted = false;
        var started = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\\')
            {
                i = Backslashes(text, i, word, ref quoted);
                started = true;
            }
            else if (c == '"')
            {
                quoted = !quoted;
                started = true;
            }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                if (started)
                {
                    words.Add(word.ToString());
                    word.Clear();
                    started = false;
                }
            }
            else
            {
                word.Append(c);
                started = true;
            }
        }
        if (quoted)
        {
            return null;
        }
        if (started)
        {
            words.Add(word.ToString());
        }
        return words;
    }

    /// <summary>
    /// A run of backslashes from <paramref name="i"/>: before a quote each pair is one backslash
    /// and an odd one escapes the quote, else they are literal. Returns the run's last index.
    /// </summary>
    private static int Backslashes(string text, int i, StringBuilder word, ref bool quoted)
    {
        var run = 0;
        while (i < text.Length && text[i] == '\\')
        {
            run++;
            i++;
        }
        if (i < text.Length && text[i] == '"')
        {
            word.Append('\\', run / 2);
            if (run % 2 == 1)
            {
                word.Append('"');
            }
            else
            {
                quoted = !quoted;
            }
            return i;
        }
        word.Append('\\', run);
        return i - 1;
    }

    private static string OutsideQuotes(string text)
    {
        var sb = new StringBuilder(text.Length);
        var quoted = false;
        foreach (var c in text)
        {
            if (c == '"')
            {
                quoted = !quoted;
            }
            else if (!quoted)
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static bool IsAssignment(string word)
    {
        var eq = word.IndexOf('=', StringComparison.Ordinal);
        return eq > 0 && word.AsSpan(0, eq).ContainsAnyExcept(NameChars) is false;
    }

    private static readonly System.Buffers.SearchValues<char> NameChars =
        System.Buffers.SearchValues.Create(
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_"
        );

    private static bool IsName(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    /// <summary><c>~</c>, <c>$NAME</c>, <c>${NAME}</c> and on Windows <c>%NAME%</c> expanded; the error names the variable.</summary>
    internal (string Value, string? Error) Expand(string word, HookSource source)
    {
        if (home.Windows)
        {
            word = Percent(word);
        }
        var sb = new StringBuilder();
        var i = 0;
        if (
            word is "~"
            || word.StartsWith("~/", StringComparison.Ordinal)
            || (home.Windows && word.StartsWith("~\\", StringComparison.Ordinal))
        )
        {
            sb.Append(home.Home);
            i = 1;
        }
        while (i < word.Length)
        {
            if (word[i] != '$')
            {
                sb.Append(word[i++]);
                continue;
            }
            var (name, next) = VariableAt(word, i + 1);
            if (name is null || name.Length == 0 || char.IsAsciiDigit(name[0]))
            {
                return (word, "`$` is not set here");
            }
            if (Value(name, source) is not { } value)
            {
                return (
                    word,
                    string.Equals(name, "CLAUDE_PLUGIN_ROOT", StringComparison.Ordinal)
                        ? "`CLAUDE_PLUGIN_ROOT` is only known inside a plugin's own hooks.json"
                        : $"`{name}` is not set here"
                );
            }
            sb.Append(value);
            i = next;
        }
        return (sb.ToString(), null);
    }

    /// <summary>The name after a <c>$</c> at <paramref name="at"/> and where it ends; a null name for an unclosed brace.</summary>
    private static (string? Name, int Next) VariableAt(string word, int at)
    {
        if (at < word.Length && word[at] == '{')
        {
            var close = word.IndexOf('}', at);
            return close < 0 ? (null, word.Length) : (word[(at + 1)..close], close + 1);
        }
        var end = at;
        while (end < word.Length && IsName(word[end]))
        {
            end++;
        }
        return (word[at..end], end);
    }

    private string? Value(string name, HookSource source) =>
        name switch
        {
            "HOME" => home.Home,
            "CLAUDE_PROJECT_DIR" => home.Cwd,
            "CLAUDE_PLUGIN_ROOT" => source.PluginRoot,
            _ => home.Var(name),
        };

    /// <summary><c>%NAME%</c> as <c>${NAME}</c>.</summary>
    private static string Percent(string word)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < word.Length; i++)
        {
            var close = word[i] == '%' ? word.IndexOf('%', i + 1) : -1;
            if (
                close > i + 1
                && word.AsSpan(i + 1, close - i - 1).ContainsAnyExcept(NameChars) is false
            )
            {
                sb.Append("${").Append(word, i + 1, close - i - 1).Append('}');
                i = close;
                continue;
            }
            sb.Append(word[i]);
        }
        return sb.ToString();
    }

    /// <summary>What actually runs: an interpreter's script, a launcher's target, else the program; direct when it is run itself.</summary>
    private (string Target, bool Direct, string? Why) Target(List<string> words)
    {
        var program = words[0];
        var name = Base(program);
        var args = words.Skip(1).ToList();
        if (home.Windows && name is "cmd")
        {
            var at = args.FindIndex(a =>
                a.Equals("/c", StringComparison.OrdinalIgnoreCase)
                || a.Equals("/k", StringComparison.OrdinalIgnoreCase)
            );
            return at >= 0 && at + 1 < args.Count
                ? Target(args[(at + 1)..])
                : ("", false, "no command after /c");
        }
        if (home.Windows && name is "powershell" or "pwsh")
        {
            var at = args.FindIndex(a =>
                a.Equals("-file", StringComparison.OrdinalIgnoreCase)
                || a.Equals("-f", StringComparison.OrdinalIgnoreCase)
            );
            return at >= 0 && at + 1 < args.Count
                ? (args[at + 1], false, null)
                : ("", false, "an inline PowerShell command");
        }
        if (Interpreters.Contains(name))
        {
            if (args.Exists(Inline.Contains))
            {
                return ("", false, "an inline script, not a file");
            }
            if (name is "deno" or "bun")
            {
                args.RemoveAll(a => a is "run");
            }
            var script = args.Find(a => !a.StartsWith('-'));
            return script is null ? ("", false, "no script argument") : (script, false, null);
        }
        var first = args.FindIndex(a => !a.StartsWith('-'));
        if (name is "npx" && first >= 0 && args[first] is "tsx")
        {
            return Target(["tsx", .. args[(first + 1)..]]);
        }
        if (name is "uv" && first >= 0 && args[first] is "run")
        {
            var rest = args[(first + 1)..];
            return rest.Count == 0
                ? ("", false, "no script argument")
                : Target(rest) with
                {
                    Direct = false,
                };
        }
        return (program, true, null);
    }

    /// <summary>The last path component, <c>.exe</c> dropped, lower case.</summary>
    internal static string Base(string program)
    {
        var name = program[(program.LastIndexOfAny(['/', '\\']) + 1)..];
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }
        return name.ToLowerInvariant();
    }

    private Check OnPosix(string target, bool direct, HookSource source)
    {
        if (!target.Contains('/', StringComparison.Ordinal) && direct)
        {
            return Builtins.Contains(target) || home.Hosts.Probe.FindOnPath(target) is not null
                ? new(Verdict.Ok, "")
                : new(Verdict.Broken, $"`{target}` not on PATH");
        }
        string path;
        if (target.StartsWith('/'))
        {
            path = target;
        }
        else if (source.RelativeOk)
        {
            path = Join(
                home.Cwd,
                target.StartsWith("./", StringComparison.Ordinal) ? target[2..] : target
            );
        }
        else
        {
            return new(
                Verdict.Unverified,
                "relative path: this host's base directory is not known"
            );
        }
        if (home.Env.Os == McpOs.MacOS && Unmounted(path) is { } volume)
        {
            return new(Verdict.Broken, $"path is on /Volumes/{volume}, which is not mounted");
        }
        return OnDisk(path, direct);
    }

    private string? Unmounted(string path)
    {
        if (!path.StartsWith("/Volumes/", StringComparison.Ordinal))
        {
            return null;
        }
        var name = path["/Volumes/".Length..].Split('/')[0];
        return name.Length > 0 && !home.Fs.Directory.Exists("/Volumes/" + name) ? name : null;
    }

    private Check OnWindows(string target, bool direct, HookSource source)
    {
        var bare =
            target.IndexOfAny(['/', '\\']) < 0
            && !target.StartsWith('~')
            && !target.StartsWith('.');
        if (bare && (Builtins.Contains(target.ToLowerInvariant()) || CmdBuiltins.Contains(target)))
        {
            return new(Verdict.Ok, "");
        }
        var exts = (home.Var("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        var hasExt = exts.Any(e => target.EndsWith(e, StringComparison.OrdinalIgnoreCase));
        List<string> bases;
        if (bare)
        {
            bases =
            [
                .. (home.Var("PATH") ?? "")
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(d => d.TrimEnd('\\') + "\\" + target),
            ];
        }
        else if (WindowsAbsolute(target))
        {
            bases = [target];
        }
        else if (source.RelativeOk)
        {
            var rest =
                target.StartsWith("./", StringComparison.Ordinal)
                || target.StartsWith(".\\", StringComparison.Ordinal)
                    ? target[2..]
                    : target;
            bases = [home.Cwd.TrimEnd('\\') + "\\" + rest.Replace('/', '\\')];
        }
        else
        {
            return new(
                Verdict.Unverified,
                "relative path: this host's base directory is not known"
            );
        }
        foreach (var b in bases)
        {
            if (
                ((IEnumerable<string>)[b, .. hasExt ? [] : exts.Select(e => b + e)]).Any(
                    home.Fs.File.Exists
                )
            )
            {
                return new(Verdict.Ok, "");
            }
        }
        return bare || bases.Count == 0
            ? new(Verdict.Broken, $"`{target}` not on PATH")
            : OnDisk(bases[0], direct: false);
    }

    private static bool WindowsAbsolute(string path) =>
        (
            path.Length >= 3
            && char.IsAsciiLetter(path[0])
            && path[1] == ':'
            && path[2] is '\\' or '/'
        ) || path.StartsWith(@"\\", StringComparison.Ordinal);

    private Check OnDisk(string path, bool direct)
    {
        var fs = home.Fs;
        if (fs.Directory.Exists(path))
        {
            return new(Verdict.Broken, $"{path} is a directory, not a script");
        }
        if (!fs.File.Exists(path))
        {
            var link = Link(path);
            return link is null
                ? new(Verdict.Broken, $"file not found: {path}")
                : new(Verdict.Broken, $"dangling symlink to {link}");
        }
        if (direct && !home.Windows && !Executable(path))
        {
            return new(Verdict.Suspect, $"{path} is not executable: run `chmod +x {path}`");
        }
        return new(Verdict.Ok, "");
    }

    private string? Link(string path)
    {
        try
        {
            return home.Fs.FileInfo.New(path).LinkTarget;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private bool Executable(string path)
    {
        try
        {
            return OperatingSystem.IsWindows()
                || (home.Fs.File.GetUnixFileMode(path) & Exec) != UnixFileMode.None;
        }
        catch (Exception e)
            when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return true;
        }
    }

    private string Join(string root, string rest) =>
        home.Windows
            ? root.TrimEnd('\\') + "\\" + rest.Replace('/', '\\')
            : root.TrimEnd('/') + "/" + rest;
}
