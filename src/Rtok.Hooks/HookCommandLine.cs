namespace Rtok.Hooks;

/// <summary>
/// The command every shipped POSIX hook runs (catalog B1, B2): the tiny <c>rtok-hook</c> first,
/// then <c>rtok hook</c>, each from PATH and then from ketch's bin directory, else a silent
/// exit 0. There is no brace group, so fish parses it too. T13's manifest generator emits it.
/// </summary>
public static class HookCommandLine
{
    /// <param name="hookEvent">The event argument, such as <c>PreToolUse</c>.</param>
    /// <param name="host">The <c>--host</c> id, or <see langword="null"/> for none.</param>
    public static string Posix(string hookEvent, string? host = null)
    {
        Check(hookEvent);
        if (host is not null)
        {
            Check(host);
        }
        var args = host is null ? hookEvent : $"{hookEvent} --host {host}";
        const string ketch = "\"$HOME/.ketch/bin";
        return $"command -v rtok-hook >/dev/null 2>&1 && exec rtok-hook {args}; "
            + $"command -v rtok >/dev/null 2>&1 && exec rtok hook {args}; "
            + $"[ -x {ketch}/rtok-hook\" ] && exec {ketch}/rtok-hook\" {args}; "
            + $"[ -x {ketch}/rtok\" ] && exec {ketch}/rtok\" hook {args}; "
            + "exit 0";
    }

    // The line is shell code: its arguments must be plain words.
    private static void Check(string word)
    {
        if (word.Length == 0 || !word.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
        {
            throw new ArgumentException($"not a plain word: '{word}'", nameof(word));
        }
    }
}
