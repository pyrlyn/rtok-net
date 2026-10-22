using System.Reflection;

namespace Rtok.Cli;

/// <summary>This build's version: the informational version is <c>&lt;version&gt;+&lt;sha9&gt;</c> (Directory.Build.targets).</summary>
internal static class BuildInfo
{
    private static readonly string Informational =
        typeof(BuildInfo)
            .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "0.0.0";

    /// <summary>The version without the commit (<c>0.1.0</c>).</summary>
    public static string Version => Informational.Split('+', 2)[0];

    /// <summary>The commit the build came from, or null.</summary>
    public static string? Commit => Informational.Split('+', 2) is [_, var sha] ? sha : null;
}
