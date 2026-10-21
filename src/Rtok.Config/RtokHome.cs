namespace Rtok.Config;

/// <summary>
/// Where rtok keeps its files: <c>$RTOK_HOME</c>, else <c>&lt;user home&gt;/.rtok</c>, and how
/// <c>~</c> in a path key expands.
/// </summary>
public static class RtokHome
{
    /// <summary>The config file's name inside the home.</summary>
    public const string ConfigFileName = "config.toml";

    /// <summary>The user home: <c>HOME</c>, else <c>USERPROFILE</c>; an empty one does not count.</summary>
    /// <param name="env">Reads an environment variable.</param>
    /// <returns>The user home, or null.</returns>
    public static string? UserHome(Func<string, string?> env)
    {
        ArgumentNullException.ThrowIfNull(env);
        return NonEmpty(env("HOME")) ?? NonEmpty(env("USERPROFILE"));
    }

    /// <summary>
    /// <c>$RTOK_HOME</c> when set, with <c>~</c> expanded, else <c>&lt;user home&gt;/.rtok</c>.
    /// The result may be relative when either input is.
    /// </summary>
    /// <param name="rtokHome">The value of <c>RTOK_HOME</c>.</param>
    /// <param name="userHome">The user home.</param>
    /// <returns>The home.</returns>
    public static string FromVariables(string? rtokHome, string? userHome)
    {
        var fallback = Path.Combine(userHome ?? "", ".rtok");
        return NonEmpty(rtokHome) is { } home ? Expand(home, fallback, userHome) : fallback;
    }

    /// <summary>
    /// The home, always absolute. A relative <c>RTOK_HOME</c> or user home resolves against the
    /// current directory; with neither set, the OS profile folder is tried, then the temp folder.
    /// </summary>
    /// <param name="env">Reads an environment variable; null reads the process environment.</param>
    /// <returns>The absolute home.</returns>
    public static string Resolve(Func<string, string?>? env = null)
    {
        env ??= Environment.GetEnvironmentVariable;
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Absolute(
            env("RTOK_HOME"),
            UserHome(env) ?? NonEmpty(profile),
            Directory.GetCurrentDirectory,
            () => Path.Combine(Path.GetTempPath(), ".rtok")
        );
    }

    /// <summary>The user config file: <paramref name="configFile"/> (<c>--config</c>), else <c>RTOK_CONFIG</c>, else <c>&lt;home&gt;/config.toml</c>.</summary>
    /// <param name="home">The rtok home.</param>
    /// <param name="configFile">The <c>--config</c> value, or null.</param>
    /// <param name="env">Reads an environment variable; null reads the process environment.</param>
    /// <returns>The file path.</returns>
    public static string UserConfigFile(
        string home,
        string? configFile,
        Func<string, string?>? env = null
    )
    {
        env ??= Environment.GetEnvironmentVariable;
        return NonEmpty(configFile)
            ?? NonEmpty(env("RTOK_CONFIG"))
            ?? Path.Combine(home, ConfigFileName);
    }

    /// <summary>
    /// <c>~/.rtok/x</c> becomes <c>&lt;rtok home&gt;/x</c>, so <c>RTOK_HOME</c> moves the whole
    /// tree; any other <c>~/x</c> becomes <c>&lt;user home&gt;/x</c>. Bare <c>~</c> and
    /// <c>~/.rtok</c> expand too. Other paths are returned as they are.
    /// </summary>
    /// <param name="path">The path from the config.</param>
    /// <param name="rtokHome">The rtok home.</param>
    /// <param name="userHome">The user home, or null to leave <c>~/x</c> literal.</param>
    /// <returns>The expanded path.</returns>
    public static string Expand(string path, string rtokHome, string? userHome)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (StripRtokHome(path) is { } rest)
        {
            return rest.Length == 0 ? rtokHome : Path.Combine(rtokHome, Native(rest));
        }
        if (path is "~")
        {
            return userHome ?? path;
        }
        if (
            path.StartsWith("~/", StringComparison.Ordinal)
            || path.StartsWith("~\\", StringComparison.Ordinal)
        )
        {
            return userHome is null ? path : Path.Combine(userHome, Native(path[2..]));
        }
        return path;
    }

    internal static string Absolute(
        string? rtokHome,
        string? userHome,
        Func<string?> cwd,
        Func<string> fallback
    )
    {
        var explicitInput = NonEmpty(rtokHome) is not null || userHome is not null;
        var home = FromVariables(rtokHome, userHome);
        if (Path.IsPathRooted(home))
        {
            return home;
        }
        if (!explicitInput)
        {
            return fallback();
        }
        return cwd() is { } dir ? Path.GetFullPath(home, dir) : home;
    }

    private static string? StripRtokHome(string raw)
    {
        foreach (
            var prefix in (ReadOnlySpan<string>)["~/.rtok/", "~/.rtok\\", "~\\.rtok\\", "~\\.rtok/"]
        )
        {
            if (raw.StartsWith(prefix, StringComparison.Ordinal))
            {
                return raw[prefix.Length..];
            }
        }
        return raw is "~/.rtok" or "~\\.rtok" ? "" : null;
    }

    private static string Native(string rest) =>
        rest.Replace(
            Path.AltDirectorySeparatorChar == '/' ? '\\' : '/',
            Path.DirectorySeparatorChar
        );

    private static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
