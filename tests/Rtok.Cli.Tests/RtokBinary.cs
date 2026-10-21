using CliWrap;
using CliWrap.Buffered;
using Pyrlyn.CommandLine.Docs;
using Rtok.Agents.Hosts;
using Rtok.Testing;

namespace Rtok.Cli.Tests;

/// <summary>The JIT-built <c>rtok</c> that the project reference copies next to the tests.</summary>
internal static class RtokBinary
{
    private static readonly Lazy<CommandModel> LazyModel = new(() =>
        CommandModelReader.Read("rtok", Run)
    );

    /// <summary>The executable's path.</summary>
    internal static string Path { get; } =
        System.IO.Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "rtok.exe" : "rtok"
        );

    /// <summary>Every command of rtok, read from its <c>--help</c>.</summary>
    internal static CommandModel Model => LazyModel.Value;

    /// <summary>Runs rtok with these arguments and returns what it printed.</summary>
    internal static string Run(IReadOnlyList<string> args) => Exec(args).Stdout;

    /// <summary>
    /// Runs rtok with these arguments, its home and the user home in an empty directory so no
    /// real config is read, agent hosts probed only under that home so no real agent is found
    /// or run, and returns its exit code and both streams.
    /// </summary>
    internal static (int Code, string Stdout, string Stderr) Exec(
        IReadOnlyList<string> args,
        IReadOnlyDictionary<string, string?>? environment = null,
        string? stdin = null
    )
    {
        var result = CliWrap
            .Cli.Wrap(Path)
            .WithArguments(args)
            .WithStandardInputPipe(stdin is null ? PipeSource.Null : PipeSource.FromString(stdin))
            .WithEnvironmentVariables(env =>
            {
                env.Set("HOME", Home.Path)
                    .Set("USERPROFILE", Home.Path)
                    .Set("RTOK_HOME", null)
                    .Set(HostEnvironment.SandboxVariable, "1");
                foreach (
                    var (name, value) in environment
                        ?? new Dictionary<string, string?>(StringComparer.Ordinal)
                )
                {
                    env.Set(name, value);
                }
            })
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync()
            .Task.GetAwaiter()
            .GetResult();
        return (result.ExitCode, result.StandardOutput, result.StandardError);
    }

    private static TempDir Home { get; } = new("rtok-cli-home-");
}
