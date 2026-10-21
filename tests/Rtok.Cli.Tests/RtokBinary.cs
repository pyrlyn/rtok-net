using CliWrap;
using CliWrap.Buffered;
using Pyrlyn.CommandLine.Docs;

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
    internal static string Run(IReadOnlyList<string> args) =>
        CliWrap
            .Cli.Wrap(Path)
            .WithArguments(args)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync()
            .Task.GetAwaiter()
            .GetResult()
            .StandardOutput;
}
