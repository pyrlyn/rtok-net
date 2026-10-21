namespace Rtok.Plugins.Cmd;

/// <summary>Flag tables of the wrappers <see cref="Families"/> peels: mise and just.</summary>
internal static class Wrappers
{
    private static readonly HashSet<string> MiseValueFlags = new(StringComparer.Ordinal)
    {
        "-C",
        "--cd",
        "-E",
        "--env",
        "-j",
        "--jobs",
        "--allow-env",
        "--allow-net",
        "--allow-read",
        "--allow-write",
    };

    private static readonly HashSet<string> MiseBoolFlags = new(StringComparer.Ordinal)
    {
        "-q",
        "-v",
        "-y",
        "--quiet",
        "--verbose",
        "--yes",
        "--locked",
        "--silent",
        "--raw",
        "--no-deps",
        "--fresh-env",
        "--deny-all",
        "--deny-env",
        "--deny-net",
        "--deny-read",
        "--deny-write",
    };

    private static readonly HashSet<string> JustValueFlags = new(StringComparer.Ordinal)
    {
        "-f",
        "--justfile",
        "-d",
        "--working-directory",
        "--color",
        "--shell",
        "--shell-arg",
        "--command-color",
        "--dotenv-filename",
        "--dotenv-command",
        "--chooser",
        "--ceiling",
        "--justfile-name",
    };

    /// <summary>The index after mise's global flags, or null on a flag it does not know.</summary>
    public static int? SkipMiseFlags(IReadOnlyList<string> argv, int i)
    {
        while (i < argv.Count)
        {
            var tok = argv[i];
            if (tok is "exec" or "x" or "run" or "r" or "--")
            {
                break;
            }

            if (MiseFlagWidth(tok) is { } n)
            {
                if (i + n > argv.Count)
                {
                    return null;
                }

                i += n;
                continue;
            }

            if (tok.StartsWith('-'))
            {
                return null;
            }

            break;
        }

        return i;
    }

    /// <summary>How many words a mise flag takes (itself included), or null for an unknown one.</summary>
    public static int? MiseFlagWidth(string tok)
    {
        if (
            Text.SplitOnce(tok, "=", out var name, out _)
            && name.StartsWith("--", StringComparison.Ordinal)
        )
        {
            return MiseFlagWidth(name) is null ? null : 1;
        }

        if (MiseValueFlags.Contains(tok))
        {
            return 2;
        }

        return MiseBoolFlags.Contains(tok) || IsVerboseShort(tok) ? 1 : null;
    }

    /// <summary>Values a just flag takes after itself.</summary>
    public static int JustFlagValues(string tok)
    {
        if (tok.Contains('=', StringComparison.Ordinal))
        {
            return 0;
        }

        return tok is "--set" ? 2
            : JustValueFlags.Contains(tok) ? 1
            : 0;
    }

    /// <summary>The command of a mise task echo, <c>[name] $ cmd</c>.</summary>
    public static string? TaskDollar(string line)
    {
        if (!line.StartsWith('[') || !Text.SplitOnce(line[1..], "] $ ", out var name, out var cmd))
        {
            return null;
        }

        return name.Length == 0 || cmd.Length == 0 ? null : cmd;
    }

    /// <summary>Whether a just recipe line reads as a command, not as that tool's output.</summary>
    public static bool LooksLikeInvocation(string[] words)
    {
        if (words.Length < 2)
        {
            return true;
        }

        var s = words[1];
        if (
            s.Contains("ERR", StringComparison.Ordinal)
            || s.Equals("error", StringComparison.OrdinalIgnoreCase)
            || s.Equals("warning", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        return !s.Contains(':', StringComparison.Ordinal)
            && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    }

    // -vv, -vvv: mise's repeated verbosity.
    private static bool IsVerboseShort(string tok) =>
        tok.Length > 2 && tok.All(c => c is '-' or 'v') && tok.EndsWith('v');
}
