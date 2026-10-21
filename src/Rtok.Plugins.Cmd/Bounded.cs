using System.Text;
using Rtok.Core;

namespace Rtok.Plugins.Cmd;

/// <summary>
/// A command the agent already bounded (rtok T176): <c>sed -n 1,80p</c>, <c>head</c>/<c>tail -n</c>,
/// <c>grep -A/-B/-C/-m</c>, <c>cat -n</c> of one named file. It printed what was asked for, and a
/// second cut only buys an <c>expand</c> round trip. The lexer reads quotes, backslash escapes and
/// the <c>|</c>, <c>|&amp;</c>, <c>&amp;&amp;</c>, <c>||</c>, <c>;</c> and newline separators and
/// nothing else of the shell grammar.
/// </summary>
internal static class Bounded
{
    /// <summary>Past this the host truncates a Bash result on its own (Claude Code: 30 000
    /// chars), and a cut that names the archive beats one that does not.</summary>
    public const int MaxBytes = 30_000;

    /// <summary>Unicode scalars the host keeps of a Bash result; a Measurement estimates this
    /// prefix, since bytes past it never reach the model.</summary>
    public const int HostVisibleChars = 30_000;

    private static readonly string[] LongContext =
    [
        "--context",
        "--after-context",
        "--before-context",
        "--max-count",
    ];

    /// <summary>The prefix the host receives: at most <see cref="HostVisibleChars"/> scalars.</summary>
    public static string HostVisiblePrefix(string text) =>
        text.Length <= HostVisibleChars ? text : Text.TakeScalars(text, HostVisibleChars);

    /// <summary>Whether every command in <paramref name="snippet"/> ends in a bounding stage. A
    /// leading <c>cd</c>/<c>export</c> prints nothing; at least one stage must bound.</summary>
    public static bool IsBounded(string snippet)
    {
        var any = false;
        foreach (var pipeline in Lex(snippet))
        {
            var last = pipeline[^1];
            if (Silent(pipeline))
            {
                continue;
            }

            if (!Bounds(last))
            {
                return false;
            }

            any = true;
        }

        return any;
    }

    /// <summary>
    /// True only when <paramref name="snippet"/> chains two or more distinct programs (rtok
    /// T177): <c>a &amp;&amp; b</c>, <c>a; b</c>, <c>a | b</c>. <c>cargo build &amp;&amp; cargo test</c>
    /// and <c>cd x &amp;&amp; cargo test</c> stay one program. Wrappers are peeled first, so
    /// <c>mise exec -- cargo &amp;&amp; mise exec -- git</c> is two.
    /// </summary>
    public static bool MixedChain(string snippet)
    {
        var programs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pipeline in Lex(snippet))
        {
            if (Silent(pipeline))
            {
                continue;
            }

            foreach (var stage in pipeline)
            {
                var visible = Families.VisibleArgv(stage);
                if (visible.Count > 0)
                {
                    programs.Add(Names.CmdStem(visible[0]));
                }
            }
        }

        return programs.Count >= 2;
    }

    /// <summary>Every stage of every pipeline in <paramref name="snippet"/>, as words with
    /// quotes removed.</summary>
    public static List<List<string>> Stages(string snippet) => [.. Lex(snippet).SelectMany(p => p)];

    private static bool Silent(List<List<string>> pipeline) =>
        pipeline.Count == 1 && pipeline[0].Count > 0 && pipeline[0][0] is "cd" or "export";

    // Pipelines of stages of words, quotes removed; pipelines with no word are dropped.
    private static List<List<List<string>>> Lex(string s) => new Lexer(s).Run();

    private static bool Bounds(List<string> stage)
    {
        if (stage.Count == 0)
        {
            return false;
        }

        var args = stage.Skip(1).ToList();
        switch (Names.CmdStem(stage[0]))
        {
            case "head":
                return Counts(args, n => n.All(char.IsAsciiDigit));
            case "tail":
                // The obsolete `tail +5 log` starts at line 5: no bound, as `-n +5`.
                return !args.Exists(a =>
                        a is "-f" or "-F" or "--follow"
                        || (a.Length > 1 && a[0] == '+' && char.IsAsciiDigit(a[1]))
                    ) && Counts(args, n => !n.StartsWith('+'));
            case "sed":
                return SedRange(args);
            case "grep":
                // -A/-B/-C bound the context per match, not the number of matches: a recursive
                // grep also needs -m/--max-count. `-r` bundles with other short flags (-rn).
                var recursive = args.Exists(a =>
                    a is "--recursive"
                    || (
                        a.StartsWith('-')
                        && !a.StartsWith("--", StringComparison.Ordinal)
                        && a.AsSpan().IndexOfAny('r', 'R') >= 0
                    )
                );
                return (!recursive || args.Exists(IsMaxCount)) && HasBoundingContext(args);
            case "rg":
                // rg is recursive by default and its -r is --replace: no recursive gate.
                return HasBoundingContext(args);
            case "cat":
                // -n only numbers lines; exactly one named file keeps a dump of many out.
                return args.Contains("-n")
                    && args.Where(a => !a.StartsWith('-')).Take(2).Count() == 1;
            default:
                return false;
        }
    }

    // grep/rg context or max-count flags (-A/-B/-C/-m N, --context=N, …).
    private static bool HasBoundingContext(List<string> args) =>
        args.Exists(a =>
            (
                Short(a) is { } s
                && s.TrimEnd("0123456789".ToCharArray()) is [.., 'A' or 'B' or 'C' or 'm']
            ) || LongContext.Contains(a.Split('=')[0])
        );

    private static bool IsMaxCount(string a) =>
        (Short(a) is { } s && s.TrimEnd("0123456789".ToCharArray()).EndsWith('m'))
        || a.Split('=')[0] is "--max-count";

    // The flag letters of a short option (`-A3` → `A3`), null for anything else.
    private static string? Short(string a) =>
        a.StartsWith('-') && !a.StartsWith("--", StringComparison.Ordinal) ? a[1..] : null;

    // head/tail counts (-n N, -nN, --lines=N, -c N, -N) all pass `ok`; no count is the
    // default ten lines, bounded too.
    private static bool Counts(List<string> args, Func<string, bool> ok)
    {
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            string? value;
            if (a is "-n" or "-c" or "--lines" or "--bytes")
            {
                value = i + 1 < args.Count ? args[++i] : null;
            }
            else
            {
                value =
                    Text.StripPrefix(a, "--lines=")
                    ?? Text.StripPrefix(a, "--bytes=")
                    ?? Text.StripPrefix(a, "-n")
                    ?? Text.StripPrefix(a, "-c")
                    ?? (a.Length > 1 && a[0] == '-' && char.IsAsciiDigit(a[1]) ? a[1..] : null);
            }

            if (value is not null && (value.Length == 0 || !ok(value)))
            {
                return false;
            }
        }

        return true;
    }

    // sed -n whose every script is numeric a[,b]p addresses, ;-joined.
    private static bool SedRange(List<string> args)
    {
        var quiet = args.Exists(a => a is "-n" or "--quiet" or "--silent");
        var scripts = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if (a is "-e" or "--expression")
            {
                if (i + 1 < args.Count)
                {
                    scripts.Add(args[++i]);
                }
            }
            else if (!a.StartsWith('-') && scripts.Count == 0)
            {
                scripts.Add(a);
            }
        }

        return quiet && scripts.Count > 0 && scripts.TrueForAll(s => s.Split(';').All(Range));
    }

    private static bool Range(string script)
    {
        var t = script.Trim();
        if (!t.EndsWith('p'))
        {
            return false;
        }

        var ends = t[..^1].Split(',');
        return ends.Length <= 2 && ends.All(e => e.Length > 0 && e.All(char.IsAsciiDigit));
    }

    // The lexer state: input, position, the word being built and the open quote.
    private sealed class Lexer(string s)
    {
        private readonly List<List<List<string>>> _lists =
        [
            [
                [],
            ],
        ];
        private StringBuilder? _word;
        private char? _quote;
        private int _i;

        public List<List<List<string>>> Run()
        {
            while (_i < s.Length)
            {
                var c = s[_i++];
                if (_quote is { } q)
                {
                    Quoted(c, q);
                }
                else
                {
                    Unquoted(c);
                }
            }

            if (_word is not null)
            {
                _lists[^1][^1].Add(_word.ToString());
            }

            return [.. _lists.Where(p => p.Exists(st => st.Count > 0))];
        }

        private char? Peek => _i < s.Length ? s[_i] : null;

        private StringBuilder Word => _word ??= new();

        private void Quoted(char c, char q)
        {
            if (c == q)
            {
                _quote = null;
                return;
            }

            if (q != '"' || c != '\\')
            {
                Word.Append(c);
                return;
            }

            // Inside double quotes only these four escape, and \-newline continues the
            // line; any other backslash stays literal.
            switch (Peek)
            {
                case '"' or '\\' or '$' or '`':
                    Word.Append(s[_i++]);
                    break;
                case '\n':
                    _i++;
                    break;
                default:
                    Word.Append(c);
                    break;
            }
        }

        private void Unquoted(char c)
        {
            switch (c)
            {
                // A backslash-newline outside quotes is a line continuation, not a separator.
                case '\\' when Peek == '\n':
                    _i++;
                    Separate(null);
                    break;
                // Any other escaped char is a plain word char: `it\'s` must not open a quote.
                case '\\':
                    Word.Append(_i < s.Length ? s[_i++] : '\\');
                    break;
                case '\'' or '"':
                    _quote = c;
                    _ = Word;
                    break;
                case '|' when Peek == '|':
                case '&' when Peek == '&':
                case ';' or '\n':
                    Separate(true);
                    break;
                case '|':
                    Separate(false);
                    break;
                case var w when char.IsWhiteSpace(w):
                    Separate(null);
                    break;
                default:
                    Word.Append(c);
                    break;
            }
        }

        // Ends the word; `newList` true starts a pipeline, false a stage, null neither.
        private void Separate(bool? newList)
        {
            var stages = _lists[^1];
            if (_word is not null)
            {
                stages[^1].Add(_word.ToString());
                _word = null;
            }

            if (newList is not { } list)
            {
                return;
            }

            if (Peek is '|' or '&')
            {
                _i++;
            }

            if (list)
            {
                _lists.Add([
                    [],
                ]);
            }
            else
            {
                stages.Add([]);
            }
        }
    }
}
