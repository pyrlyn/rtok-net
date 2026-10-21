using System.Runtime.InteropServices;
using CliWrap;
using CliWrap.Buffered;

namespace Rtok.Testing;

/// <summary>
/// The NativeAOT executables the smoke lane and the latency harness drive. With
/// <c>RTOK_SMOKE_BIN</c> set (CI publishes once per RID) they are taken from that directory;
/// otherwise each app is published here once per process, into its own artifacts root so the
/// RID-specific restore never touches the JIT build's <c>obj/</c>. <c>rtok+sample</c> is
/// <c>rtok</c> with <c>tests/Rtok.SamplePlugin</c> linked in (N12); it is always published here.
/// </summary>
public static class PublishedApps
{
    /// <summary>A directory that already holds the published executables.</summary>
    public const string BinDirVariable = "RTOK_SMOKE_BIN";

    /// <summary><c>rtok</c> built with the sample out-of-tree plugin.</summary>
    public const string RtokWithSample = "rtok+sample";

    private static readonly Dictionary<string, App> Apps = new(StringComparer.Ordinal)
    {
        ["rtok"] = new("rtok", "src/Rtok.Cli/Rtok.Cli.csproj", null),
        ["rtok-hook"] = new("rtok-hook", "src/Rtok.Hook/Rtok.Hook.csproj", null),
        ["Rtok.AotTests"] = new("Rtok.AotTests", "tests/Rtok.AotTests/Rtok.AotTests.csproj", null),
        [RtokWithSample] = new(
            "rtok",
            "src/Rtok.Cli/Rtok.Cli.csproj",
            "tests/Rtok.SamplePlugin/rtok-plugins.props"
        ),
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
    /// (<c>rtok</c>, <c>rtok-hook</c>, <c>Rtok.AotTests</c> or <see cref="RtokWithSample"/>).</summary>
    public static async Task<string> PathAsync(string app, CancellationToken ct = default)
    {
        if (!Apps.TryGetValue(app, out var spec))
        {
            throw new ArgumentException($"unknown app {app}", nameof(app));
        }

        var exe = OperatingSystem.IsWindows() ? spec.Exe + ".exe" : spec.Exe;
        var binDir = Environment.GetEnvironmentVariable(BinDirVariable);
        if (!string.IsNullOrEmpty(binDir) && spec.PluginsFile is null)
        {
            return Path.GetFullPath(Path.Join(binDir, exe));
        }

        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!Published.TryGetValue(app, out var path))
            {
                path = Path.Join(await PublishAsync(spec, ct).ConfigureAwait(false), exe);
                Published[app] = path;
            }

            return path;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<string> PublishAsync(App app, CancellationToken ct)
    {
        // A plugin build gets its own root: its obj/ holds a different LinkedPlugins.
        var artifacts = Path.Join(
            RepoPaths.Root,
            "artifacts",
            app.PluginsFile is null ? "smoke" : "smoke-plugins"
        );
        var name = Path.GetFileNameWithoutExtension(app.Project);
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        List<string> arguments =
        [
            "publish",
            Path.Join(RepoPaths.Root, app.Project),
            "-c",
            "Release",
            "-r",
            Rid,
            "--artifacts-path",
            artifacts,
            "-nologo",
        ];
        if (app.PluginsFile is not null)
        {
            arguments.Add("-p:RtokPluginsFile=" + Path.Join(RepoPaths.Root, app.PluginsFile));
        }

        var result = await Cli.Wrap(dotnet)
            .WithArguments(arguments)
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
                $"dotnet publish {app.Project} exited {result.ExitCode}:\n{result.StandardOutput}{result.StandardError}"
            );
        }

        return Path.Join(artifacts, "publish", name, $"release_{Rid}");
    }

    private sealed record App(string Exe, string Project, string? PluginsFile);
}
