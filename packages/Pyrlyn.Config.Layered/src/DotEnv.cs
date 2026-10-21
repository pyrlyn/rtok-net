using System.Text;

namespace Pyrlyn.Config.Layered;

/// <summary>
/// Reads <c>.env</c> files without touching the process environment. Lines are
/// <c>NAME=value</c>, optionally after <c>export</c>; values may be single-quoted (literal),
/// double-quoted (escapes, may span lines) or bare (a <c> #</c> starts a comment). Variables
/// are not expanded.
/// </summary>
public static class DotEnv
{
    /// <summary>
    /// The nearest <paramref name="name"/> file at or above <paramref name="start"/>. Only a
    /// file counts: a directory with that name does not stop the walk.
    /// </summary>
    /// <param name="start">The directory to start in.</param>
    /// <param name="name">The file name.</param>
    /// <returns>The file's full path, or null.</returns>
    public static string? FindUp(string start, string name = ".env")
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            var path = Path.Join(dir.FullName, name);
            if (System.IO.File.Exists(path))
            {
                return path;
            }
        }
        return null;
    }

    /// <summary>
    /// Parses <c>.env</c> text. A malformed line stops the parse: the assignments before it
    /// are kept and the error is returned.
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <param name="file">The file, for locations.</param>
    /// <returns>The assignments in file order, and the error or null.</returns>
    public static (IReadOnlyList<DotEnvVariable> Variables, ConfigFileException? Error) Parse(
        string text,
        string file
    )
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var vars = new List<DotEnvVariable>();
        for (var i = 0; i < lines.Length; i++)
        {
            var start = i;
            var line = lines[i].Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }
            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line[7..].TrimStart();
            }
            var eq = line.IndexOf('=', StringComparison.Ordinal);
            var name = eq < 0 ? line : line[..eq].TrimEnd();
            if (eq < 0 || !IsName(name))
            {
                var error = $"expected NAME=value, got: {lines[i].Trim()}";
                return (vars, new ConfigFileException(file, start + 1, 0, error));
            }
            var rest = line[(eq + 1)..].TrimStart();
            string? value;
            if (rest.Length > 0 && rest[0] is '"' or '\'')
            {
                value = Quoted(lines, ref i, rest, out var error);
                if (value is null)
                {
                    return (vars, new ConfigFileException(file, i + 1, 0, error!));
                }
            }
            else
            {
                var hash = rest.IndexOf(" #", StringComparison.Ordinal);
                value = (hash < 0 ? rest : rest[..hash]).TrimEnd();
            }
            vars.Add(new DotEnvVariable(name, value, file, start + 1));
        }
        return (vars, null);
    }

    /// <summary>A quoted value starting at <paramref name="rest"/>; it may run over later lines, which moves <paramref name="i"/>.</summary>
    private static string? Quoted(string[] lines, ref int i, string rest, out string? error)
    {
        var quote = rest[0];
        var body = new StringBuilder();
        var tail = rest[1..];
        var start = i;
        int close;
        while ((close = ClosingQuote(tail, quote)) < 0)
        {
            if (++i >= lines.Length)
            {
                i = start;
                error = $"unterminated {quote} quote";
                return null;
            }
            body.Append(tail).Append('\n');
            tail = lines[i];
        }
        body.Append(tail[..close]);
        var after = tail[(close + 1)..].Trim();
        if (after.Length > 0 && after[0] != '#')
        {
            error = $"unexpected text after the closing quote: {after}";
            return null;
        }
        error = null;
        return quote == '"' ? Unescape(body.ToString()) : body.ToString();
    }

    private static bool IsName(string name) =>
        name.Length > 0
        && (char.IsAsciiLetter(name[0]) || name[0] == '_')
        && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-');

    private static int ClosingQuote(string s, char quote)
    {
        for (var i = 0; i < s.Length; i++)
        {
            if (quote == '"' && s[i] == '\\')
            {
                i++;
            }
            else if (s[i] == quote)
            {
                return i;
            }
        }
        return -1;
    }

    private static string Unescape(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] != '\\' || i + 1 == s.Length)
            {
                sb.Append(s[i]);
                continue;
            }
            var c = s[++i];
            sb.Append(
                c switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => c,
                }
            );
        }
        return sb.ToString();
    }
}
