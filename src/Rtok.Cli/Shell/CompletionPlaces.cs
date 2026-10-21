using Rtok.Hook.Contracts;

namespace Rtok.Cli.Shell;

/// <summary>
/// Where each shell looks for per-user completions. <see cref="FromEnvironment"/> reads the
/// process; tests build one over a temp dir so no real home is touched.
/// </summary>
internal sealed record CompletionPlaces
{
    /// <summary>The user home.</summary>
    public required string Home { get; init; }

    /// <summary><c>$XDG_DATA_HOME</c>.</summary>
    public string? XdgData { get; init; }

    /// <summary><c>$XDG_CONFIG_HOME</c>.</summary>
    public string? XdgConfig { get; init; }

    /// <summary><c>$ZDOTDIR</c>.</summary>
    public string? ZDotDir { get; init; }

    /// <summary><c>%LOCALAPPDATA%</c>.</summary>
    public string? LocalAppData { get; init; }

    /// <summary><c>$SHELL</c>, for picking a shell when none is named.</summary>
    public string? Shell { get; init; }

    /// <summary>Whether this is Windows, where PowerShell's profile lives under Documents.</summary>
    public bool Windows { get; init; }

    /// <summary>The places of this process.</summary>
    /// <returns>The places.</returns>
    public static CompletionPlaces FromEnvironment()
    {
        static string? Var(string name) =>
            Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;
        return new CompletionPlaces
        {
            Home =
                RtokHome.UserHome(Environment.GetEnvironmentVariable)
                ?? throw new InvalidOperationException("no HOME or USERPROFILE"),
            XdgData = Var("XDG_DATA_HOME"),
            XdgConfig = Var("XDG_CONFIG_HOME"),
            ZDotDir = Var("ZDOTDIR"),
            LocalAppData = Var("LOCALAPPDATA"),
            Shell = Environment.GetEnvironmentVariable("SHELL"),
            Windows = OperatingSystem.IsWindows(),
        };
    }

    /// <summary>The named shell, else the one <c>$SHELL</c> names.</summary>
    /// <param name="shell">The shell the user named, or null.</param>
    /// <returns>One of <see cref="CommandDocs.Shells"/>.</returns>
    public string Pick(string? shell)
    {
        var name =
            shell ?? (Shell is { Length: > 0 } s ? Path.GetFileNameWithoutExtension(s) : null);
        return name is not null && CommandDocs.Shells.Contains(name, StringComparer.Ordinal)
            ? name
            : throw new InvalidOperationException(
                $"cannot tell the shell from $SHELL; name one of: {string.Join(", ", CommandDocs.Shells)}"
            );
    }

    /// <summary>The script's path and, for a shell that needs one, how the shell finds it.</summary>
    /// <param name="shell">One of <see cref="CommandDocs.Shells"/>.</param>
    /// <returns>The path and the note.</returns>
    public (string Path, string? Note) Script(string shell)
    {
        switch (shell)
        {
            case "bash":
                return (
                    Path.Join(
                        XdgData ?? Join(Home, ".local", "share"),
                        "bash-completion",
                        "completions",
                        "rtok"
                    ),
                    null
                );
            case "zsh":
                var dir = Path.Join(ZDotDir ?? Home, ".zfunc");
                return (
                    Path.Join(dir, "_rtok"),
                    $"zsh loads it when .zshrc has: fpath+=({dir}); autoload -Uz compinit && compinit"
                );
            case "fish":
                return (Join(Config, "fish", "completions", "rtok.fish"), null);
            case "powershell":
                return (Path.Join(Path.GetDirectoryName(PowerShellProfile), "rtok.ps1"), null);
            case "clink":
                return LocalAppData is null
                    ? throw new InvalidOperationException(
                        "clink needs %LOCALAPPDATA% (Clink runs on Windows)"
                    )
                    : (Join(LocalAppData, "clink", "rtok.lua"), null);
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(shell),
                    shell,
                    "not a shell rtok completes for"
                );
        }
    }

    /// <summary>
    /// pwsh's <c>$PROFILE</c> (CurrentUserCurrentHost). A Documents folder moved elsewhere
    /// (OneDrive) is not followed; add the script to <c>$PROFILE</c> by hand there.
    /// </summary>
    public string PowerShellProfile =>
        Join(
            Windows ? Join(Home, "Documents", "PowerShell") : Join(Config, "powershell"),
            "Microsoft.PowerShell_profile.ps1"
        );

    private string Config => XdgConfig ?? Join(Home, ".config");

    private static string Join(params string[] parts) => Path.Join(parts);
}
