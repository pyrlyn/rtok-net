using System.Runtime.InteropServices;
using CliWrap;
using CliWrap.Buffered;
using CliWrap.Builders;

namespace Rtok.Testing;

/// <summary>
/// The NativeAOT executables the smoke lane and the latency harness drive. With
/// <c>RTOK_SMOKE_BIN</c> set (CI publishes once per RID) they are taken from that directory;
/// otherwise each app is published here once per process, into its own artifacts root so the
/// RID-specific restore never touches the JIT build's <c>obj/</c>. The plugin variants pick
/// their plugins at build time (N12), so they are always published here, each into its own root:
/// <c>rtok+measure</c> links only the measure plugin, <c>rtok+sample</c> only
/// <c>tests/Rtok.SamplePlugin</c>.
/// </summary>
public static class PublishedApps
{
    /// <summary>A directory that already holds the published executables.</summary>
    public const string BinDirVariable = "RTOK_SMOKE_BIN";

    /// <summary><c>rtok</c> built with the sample out-of-tree plugin and no built-in one.</summary>
    public const string RtokWithSample = "rtok+sample";

    /// <summary><c>rtok</c> built with <c>-p:RtokPlugins=measure</c>.</summary>
    public const string RtokMeasureOnly = "rtok+measure";

    private static readonly Dictionary<string, App> Apps = new(StringComparer.Ordinal)
    {
        ["rtok"] = new("rtok", "src/Rtok.Cli/Rtok.Cli.csproj", null, null),
        ["rtok-hook"] = new("rtok-hook", "src/Rtok.Hook/Rtok.Hook.csproj", null, null),
        ["Rtok.AotTests"] = new(
            "Rtok.AotTests",
            "tests/Rtok.AotTests/Rtok.AotTests.csproj",
            null,
            null
        ),
        [RtokWithSample] = new(
            "rtok",
            "src/Rtok.Cli/Rtok.Cli.csproj",
            "none",
            "tests/Rtok.SamplePlugin/rtok-plugins.props"
        ),
        [RtokMeasureOnly] = new("rtok", "src/Rtok.Cli/Rtok.Cli.csproj", "measure", null),
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

    // How long a publish waits for another test process's publish to finish.
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(30);
    private static readonly Dictionary<string, string> Published = new(StringComparer.Ordinal);

    /// <summary>The runtime identifier the apps are published for: the host's.</summary>
    public static string Rid => RuntimeInformation.RuntimeIdentifier;

    /// <summary>The full path of the published executable <paramref name="app"/>
    /// (<c>rtok</c>, <c>rtok-hook</c>, <c>Rtok.AotTests</c>, <see cref="RtokWithSample"/> or
    /// <see cref="RtokMeasureOnly"/>).</summary>
    public static async Task<string> PathAsync(string app, CancellationToken ct = default)
    {
        if (!Apps.TryGetValue(app, out var spec))
        {
            throw new ArgumentException($"unknown app {app}", nameof(app));
        }

        var exe = OperatingSystem.IsWindows() ? spec.Exe + ".exe" : spec.Exe;
        var binDir = Environment.GetEnvironmentVariable(BinDirVariable);
        if (!string.IsNullOrEmpty(binDir) && !spec.PicksPlugins)
        {
            return Path.GetFullPath(Path.Join(binDir, exe));
        }

        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!Published.TryGetValue(app, out var path))
            {
                using (await LockAsync(ct).ConfigureAwait(false))
                {
                    path = Path.Join(await PublishAsync(app, spec, ct).ConfigureAwait(false), exe);
                }

                Published[app] = path;
            }

            return path;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// The publish lock shared by every process on this checkout: <c>dotnet test</c> runs each
    /// test project in its own process, and they publish into the same <c>artifacts/smoke*</c>
    /// roots. Whoever holds it publishes; the others wait, and their publish then finds the
    /// output up to date. Dispose to release; the OS releases it if the process dies.
    /// </summary>
    public static Task<IDisposable> LockAsync(CancellationToken ct = default) =>
        LockAsync(Path.Join(RepoPaths.Root, "artifacts", "smoke.lock"), LockTimeout, ct);

    // An exclusive open of `path`, retried until `timeout`; then the sharing IOException.
    internal static async Task<IDisposable> LockAsync(
        string path,
        TimeSpan timeout,
        CancellationToken ct
    )
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            try
            {
                return new FileStream(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None
                );
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary><c>dotnet</c> run from the repo root with this process's MSBuild variables
    /// cleared, so a nested build finds its own, plus <paramref name="env"/>; the exit code is
    /// the caller's to check.</summary>
    public static Command Dotnet(
        IEnumerable<string> arguments,
        Action<EnvironmentVariablesBuilder>? env = null
    ) =>
        Cli.Wrap(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            .WithArguments(arguments)
            .WithWorkingDirectory(RepoPaths.Root)
            .WithEnvironmentVariables(builder =>
            {
                foreach (var variable in MsBuildVariables)
                {
                    builder.Set(variable, null);
                }

                env?.Invoke(builder);
            })
            .WithValidation(CommandResultValidation.None);

    private static async Task<string> PublishAsync(string key, App app, CancellationToken ct)
    {
        // A plugin build gets its own root: its obj/ holds a different LinkedPlugins.
        var artifacts = Path.Join(
            RepoPaths.Root,
            "artifacts",
            app.PicksPlugins
                ? "smoke-" + key[(key.IndexOf('+', StringComparison.Ordinal) + 1)..]
                : "smoke"
        );
        var name = Path.GetFileNameWithoutExtension(app.Project);
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
        if (app.Plugins is not null)
        {
            arguments.Add("-p:RtokPlugins=" + app.Plugins);
        }

        if (app.PluginsFile is not null)
        {
            arguments.Add("-p:RtokPluginsFile=" + Path.Join(RepoPaths.Root, app.PluginsFile));
        }

        var result = await Dotnet(arguments).ExecuteBufferedAsync(ct).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet publish {app.Project} exited {result.ExitCode}:\n{result.StandardOutput}{result.StandardError}"
            );
        }

        return Path.Join(artifacts, "publish", name, $"release_{Rid}");
    }

    // Plugins: the built-in ids (RtokPlugins); PluginsFile: out-of-tree plugins (RtokPluginsFile).
    private sealed record App(string Exe, string Project, string? Plugins, string? PluginsFile)
    {
        public bool PicksPlugins => Plugins is not null || PluginsFile is not null;
    }
}
