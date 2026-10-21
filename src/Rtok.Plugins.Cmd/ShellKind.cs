namespace Rtok.Plugins.Cmd;

/// <summary>Which launcher <c>rtok run</c> uses for a shell.</summary>
internal enum ShellKind
{
    /// <summary><c>sh -c</c> and its kin.</summary>
    Posix,

    /// <summary><c>cmd.exe /D /C</c>.</summary>
    Cmd,

    /// <summary><c>powershell</c> / <c>pwsh -Command</c>.</summary>
    PowerShell,
}
