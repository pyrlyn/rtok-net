namespace Rtok.Plugins.Cmd;

/// <summary>Reads one compiler, linter or test diagnostic line as (key, message, location).</summary>
internal static class Diagnostics
{
    /// <summary>
    /// <c>error[E0308]: msg</c>, <c>error TS2322</c> / <c>error CS0103</c>, <c>1:2 error msg rule</c>,
    /// located linter lines (<see cref="ParseLocated"/>), <c>FAILED loc - Exc: msg</c> and
    /// <c>Exc: msg</c>; null for any other line.
    /// </summary>
    public static (string Key, string Msg, string Loc)? Parse(string line)
    {
        var t = line.Trim();
        var p = t.IndexOf("error[", StringComparison.Ordinal);
        if (p < 0)
        {
            p = t.IndexOf("warning[", StringComparison.Ordinal);
        }

        if (p >= 0)
        {
            var kindEnd = t.IndexOf('[', p) + 1;
            var end = t.IndexOf(']', kindEnd);
            if (end < 0 || end == kindEnd)
            {
                return null;
            }

            return (t[kindEnd..end], t[(end + 1)..].TrimStart(':', ' ').Trim(), "");
        }

        if ((Coded(t, "error TS", "TS") ?? Coded(t, "error CS", "CS")) is { } coded)
        {
            return coded;
        }

        var parts = Text.Words(t);
        if (parts.Length >= 4 && parts[1] is "error" or "warning")
        {
            var loc = parts[0];
            if (!Text.SplitOnce(loc, ":", out var a, out var b))
            {
                return null;
            }

            if (a.All(char.IsAsciiDigit) && b.All(char.IsAsciiDigit))
            {
                return (parts[^1], string.Join(' ', parts[2..^1]), loc);
            }
        }

        if (ParseLocated(t) is { } located)
        {
            return located;
        }

        return Raised(t);
    }

    // `FAILED loc - Exc: msg` (pytest) and `Exc: msg`, an `E ` prefix stripped.
    private static (string Key, string Msg, string Loc)? Raised(string t)
    {
        if (
            Text.StripPrefix(t, "FAILED ") is { } failed
            && Text.SplitOnce(failed, " - ", out var at, out var err)
            && Text.SplitOnce(err, ": ", out var cls, out var msg)
            && IsException(cls)
        )
        {
            return (cls, msg, at);
        }

        if (Text.SplitOnce(t, ": ", out var head, out var message))
        {
            var exc = head.Trim();
            while (exc.StartsWith("E ", StringComparison.Ordinal))
            {
                exc = exc[2..];
            }

            exc = exc.Trim();
            if (IsException(exc))
            {
                return (exc, message, "");
            }
        }

        return null;
    }

    /// <summary>
    /// ruff/flake8 (<c>f.py:3:8: F401 [*] msg</c>), mypy (<c>f.py:3: error: msg  [arg-type]</c>),
    /// rubocop (<c>f.rb:3:5: C: Style/Foo: msg</c>) and golangci-lint (<c>f.go:3:2: msg (errcheck)</c>).
    /// The rule name is the key and <c>path:line[:col]</c> the location (rtok T662).
    /// </summary>
    private static (string Key, string Msg, string Loc)? ParseLocated(string t)
    {
        // path:line or path:line:col; a bare word or a `key: value` line is not a diagnostic.
        if (
            !Text.SplitOnce(t, ": ", out var loc, out var rest)
            || !Text.RSplitOnce(loc, ":", out var path, out var last)
            || path.Length == 0
            || loc.Any(char.IsWhiteSpace)
            || last.Length == 0
            || !last.All(char.IsAsciiDigit)
        )
        {
            return null;
        }

        rest = rest.Trim();
        var words = Text.Words(rest);
        if (words.Length == 0)
        {
            return null;
        }

        var code = words[0];
        if (
            code.Length is >= 2 and <= 7
            && char.IsAsciiLetterUpper(code[0])
            && code.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c))
            && code.Any(char.IsAsciiDigit)
        )
        {
            var msg = rest[code.Length..].TrimStart();
            while (msg.StartsWith("[*]", StringComparison.Ordinal))
            {
                msg = msg[3..];
            }

            return (code, msg.Trim(), loc);
        }

        return Rubocop(rest, loc) ?? Bracketed(rest, loc) ?? Parenthesized(rest, loc);
    }

    // rubocop: `C: Style/Foo: msg`, `W: [Correctable] Lint/Bar: msg`.
    private static (string, string, string)? Rubocop(string rest, string loc)
    {
        if (
            Text.SplitOnce(rest, ": ", out var severity, out var body)
            && severity.Length == 1
            && char.IsAsciiLetterUpper(severity[0])
        )
        {
            body = Text.StripPrefix(body, "[Correctable] ") ?? body;
            if (
                Text.SplitOnce(body, ": ", out var cop, out var msg)
                && cop.Contains('/', StringComparison.Ordinal)
                && cop.Length > 0
                && char.IsAsciiLetterUpper(cop[0])
                && !cop.Any(char.IsWhiteSpace)
            )
            {
                return (cop, msg.Trim(), loc);
            }
        }

        return null;
    }

    // mypy: `error: msg  [arg-type]`.
    private static (string, string, string)? Bracketed(string rest, string loc)
    {
        var msg = Text.StripPrefix(rest, "error: ") ?? Text.StripPrefix(rest, "warning: ");
        if (msg is null || !msg.EndsWith(']'))
        {
            return null;
        }

        var open = msg.LastIndexOf("  [", StringComparison.Ordinal);
        if (open < 0)
        {
            return null;
        }

        var key = msg[(open + 3)..^1];
        return key.Length > 0 && !key.Any(char.IsWhiteSpace)
            ? (key, msg[..open].Trim(), loc)
            : null;
    }

    // golangci-lint: `msg (errcheck)`.
    private static (string, string, string)? Parenthesized(string rest, string loc)
    {
        if (!rest.EndsWith(')'))
        {
            return null;
        }

        var open = rest.LastIndexOf(" (", StringComparison.Ordinal);
        if (open < 0)
        {
            return null;
        }

        var key = rest[(open + 2)..^1];
        return
            key.Length > 0
            && key.All(c => char.IsAsciiLetterLower(c) || c == '-' || char.IsAsciiDigit(c))
            ? (key, rest[..open].Trim(), loc)
            : null;
    }

    private static (string, string, string)? Coded(string t, string needle, string prefix)
    {
        var p = t.IndexOf(needle, StringComparison.Ordinal);
        if (p < 0)
        {
            return null;
        }

        var rest = t[(p + needle.Length)..];
        var n = 0;
        while (n < rest.Length && char.IsAsciiDigit(rest[n]))
        {
            n++;
        }

        return n == 0
            ? null
            : (prefix + rest[..n], rest[n..].TrimStart(':', ' ').Trim(), LocBefore(t, p));
    }

    // `file.cs(3,7)` becomes `file.cs:3`; any other text before the code stays as written.
    private static string LocBefore(string t, int p)
    {
        var s = t[..p].Trim().TrimEnd(':').Trim();
        if (!Text.SplitOnce(s, "(", out var file, out var rest))
        {
            return s;
        }

        var end = rest.IndexOfAny([',', ')']);
        var line = end < 0 ? rest : rest[..end];
        return file.Length == 0 || line.Length == 0 ? s : $"{file}:{line}";
    }

    private static bool IsException(string s) =>
        s.Length > 0
        && char.IsAsciiLetterUpper(s[0])
        && s.All(char.IsAsciiLetterOrDigit)
        && (
            s.EndsWith("Error", StringComparison.Ordinal)
            || s.EndsWith("Exception", StringComparison.Ordinal)
            || s.EndsWith("Warning", StringComparison.Ordinal)
        );
}
