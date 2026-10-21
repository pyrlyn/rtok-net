using System.Runtime.InteropServices;
using CliWrap;
using CliWrap.Buffered;

namespace Rtok.Testing;

/// <summary>
/// The NativeAOT executables the smoke lane and the latency harness drive. With
/// <c>RTOK_SMOKE_BIN</c> set (CI publishes once per RID) they are taken from that directory;
/// otherwise each app is published here once per process, into its own artifacts root so the
/// RID-specific restore never touches the JIT build's <c>obj/</c>.
/// </summary>
public static class PublishedApps
{
    /// <summary>A directory that already holds the published executables.</summary>
    public const string BinDirVariable = "RTOK_SMOKE_BIN";

    private static readonly Dictionary<string, string> Projects = new(StringComparer.Ordinal)
    {
        ["rtok"] = "src/Rtok.Cli/Rtok.Cli.csproj",
        ["rtok-hook"] = "src/Rtok.Hook/Rtok.Hook.csproj",
        ["Rtok.AotTests"] = "tests/Rtok.AotTests/Rtok.AotTests.csproj",
    };

    // dotnet test sets these for its own MSBuild; a nested dotnet publish must find its own.
    private static readonly string[] MsBuildVariables =
    [
        "MSBuildExtensionsPath",
        "MSBuildSDKsPath",
        "MSBUILD_EXE_PATH",
        "MSBuildLoadMicrosoftTargetsReadOnly",
    ];

    // One publish at a time: the apps will share project references.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<string, string> Published = new(StringComparer.Ordinal);

    /// <summary>The runtime identifier the apps are published for: the host's.</summary>
    public static string Rid => RuntimeInformation.RuntimeIdentifier;

    /// <summary>The full path of the published executable <paramref name="app"/>
    /// (<c>rtok</c>, <c>rtok-hook</c> or <c>Rtok.AotTests</c>).</summary>
    public static async Task<string> PathAsync(string app, CancellationToken ct = default)
    {
        var exe = OperatingSystem.IsWindows() ? app + ".exe" : app;
        var binDir = Environment.GetEnvironmentVariable(BinDirVariable);
        if (!string.IsNullOrEmpty(binDir))
        {
            return Path.GetFullPath(Path.Join(binDir, exe));
        }

        if (!Projects.TryGetValue(app, out var project))
        {
            throw new ArgumentException($"unknown app {app}", nameof(app));
        }

        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!Published.TryGetValue(app, out var path))
            {
                path = Path.Join(await PublishAsync(project, ct).ConfigureAwait(false), exe);
                Published[app] = path;
            }

            return path;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<string> PublishAsync(string project, CancellationToken ct)
    {
        var artifacts = Path.Join(RepoPaths.Root, "artifacts", "smoke");
        var name = Path.GetFileNameWithoutExtension(project);
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        var result = await Cli.Wrap(dotnet)
            .WithArguments([
                "publish",
                Path.Join(RepoPaths.Root, project),
                "-c",
                "Release",
                "-r",
                Rid,
                "--artifacts-path",
                artifacts,
                "-nologo",
            ])
            .WithWorkingDirectory(RepoPaths.Root)
            .WithEnvironmentVariables(env =>
            {
                foreach (var variable in MsBuildVariables)
                {
                    env.Set(variable, null);
                }
            })
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(ct)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet publish {project} exited {result.ExitCode}:\n{result.StandardOutput}{result.StandardError}"
            );
        }

        return Path.Join(artifacts, "publish", name, $"release_{Rid}");
    }
}
