namespace Rtok.Hook.Contracts;

/// <summary>
/// The hook lines that find <c>rtok</c> when the hook runs, not when it is written (rtok
/// T174/T250.2): <c>PATH</c>, then ketch's <c>~/.ketch/bin/rtok</c>, then exit 0. Host configs
/// and the plugin manifests share these bytes.
/// </summary>
public static class HookLines
{
    /// <summary>
    /// The POSIX line running <c>rtok <paramref name="args"/></c>; when rtok is nowhere it prints
    /// <paramref name="note"/>, a JSON reply without <c>'</c>, or nothing.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="note"/> holds a <c>'</c>.</exception>
    public static string Posix(string args, string? note = null) =>
        $"command -v rtok >/dev/null 2>&1 && exec rtok {args}; "
        + $"[ -x \"$HOME/.ketch/bin/rtok\" ] && exec \"$HOME/.ketch/bin/rtok\" {args}; "
        + $"true{(note is null ? "" : $" && printf '%s' '{Quoted(note)}'")}; exit 0";

    /// <summary>
    /// <see cref="Posix"/> for PowerShell: <c>&amp; $r</c> with no pipeline keeps stdin, and
    /// <paramref name="note"/> is single-quoted.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="note"/> holds a <c>'</c>.</exception>
    public static string PowerShell(string args, string? note = null) =>
        "$r = (Get-Command rtok -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source; "
        + "if (-not $r) { $k = Join-Path $env:USERPROFILE '.ketch\\bin\\rtok.exe'; if (Test-Path -LiteralPath $k) { $r = $k } }; "
        + $"if ($r) {{ & $r {args}; exit $LASTEXITCODE }}; "
        + (note is null ? "exit 0" : $"'{Quoted(note)}'; exit 0");

    private static string Quoted(string note) =>
        note.Contains('\'', StringComparison.Ordinal)
            ? throw new ArgumentException("a hook note cannot hold '", nameof(note))
            : note;
}
