using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Cake.Core;
using Cake.Frosting;
using CliWrap;
using CliWrap.Buffered;

namespace Rtok.Build;

/// <summary>What every task shares: the repo root, arguments, RIDs and running tools.</summary>
public sealed partial class BuildContext(ICakeContext context) : FrostingContext(context)
{
    /// <summary>The checkout this tool was built from (the directory holding rtok.slnx).</summary>
    public string Root { get; } = FindRoot();

    /// <summary>A <c>--name=value</c> argument, or "" when it is missing.</summary>
    public string Arg(string name) =>
        Arguments.HasArgument(name) ? Arguments.GetArgument(name) ?? "" : "";

    /// <summary>A <c>--name</c> switch.</summary>
    public bool Flag(string name) =>
        Arguments.HasArgument(name) && Arguments.GetArgument(name) is not "false";

    /// <summary>The <c>--rid</c> argument, one of the RIDs rtok ships.</summary>
    public string Rid(string usage)
    {
        var rid = Arg("rid");
        return RidPattern().IsMatch(rid) ? rid : throw Usage(usage);
    }

    /// <summary>A path argument, absolute against the directory the tool was started in.</summary>
    public string PathArg(string name) => Arg(name) is "" ? "" : Path.GetFullPath(Arg(name));

    public static string Exe(string rid) =>
        rid.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : "";

    public static bool IsMusl(string rid) =>
        rid.StartsWith("linux-musl-", StringComparison.Ordinal);

    /// <summary>On Alpine a musl RID builds and runs natively; elsewhere it goes through docker.</summary>
    public static bool OnAlpine => File.Exists("/etc/alpine-release");

    public static CakeException Usage(string usage) => new("usage: " + usage);

    public static CakeException Fail(string message) => new(message);

    /// <summary>Runs a tool with its output on this console; a non-zero exit throws.</summary>
    public async Task RunAsync(
        string exe,
        IEnumerable<string> args,
        string? workDir = null,
        PipeSource? stdin = null
    )
    {
        using var stdout = Console.OpenStandardOutput();
        using var stderr = Console.OpenStandardError();
        await Cli.Wrap(exe)
            .WithArguments(args)
            .WithWorkingDirectory(workDir ?? Root)
            .WithStandardInputPipe(stdin ?? PipeSource.Null)
            .WithStandardOutputPipe(PipeTarget.ToStream(stdout))
            .WithStandardErrorPipe(PipeTarget.ToStream(stderr))
            .ExecuteAsync();
    }

    /// <summary>Runs a tool and returns what it wrote; a non-zero exit throws unless allowed.</summary>
    public async Task<BufferedCommandResult> CaptureAsync(
        string exe,
        IEnumerable<string> args,
        PipeSource? stdin = null,
        bool allowFailure = false
    ) =>
        await Cli.Wrap(exe)
            .WithArguments(args)
            .WithWorkingDirectory(Root)
            .WithStandardInputPipe(stdin ?? PipeSource.Null)
            .WithValidation(
                allowFailure ? CommandResultValidation.None : CommandResultValidation.ZeroExitCode
            )
            .ExecuteBufferedAsync();

    /// <summary>The short commit the version option prints; taken here, where git sees the checkout.</summary>
    public async Task<string> RevisionAsync()
    {
        if (Environment.GetEnvironmentVariable("RTOK_SOURCE_REVISION") is { Length: > 0 } revision)
            return revision;
        try
        {
            var git = await CaptureAsync(
                "git",
                ["rev-parse", "--short=9", "HEAD"],
                allowFailure: true
            );
            return git.ExitCode == 0 ? git.StandardOutput.Trim() : "unknown";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return "unknown"; // no git on PATH
        }
    }

    /// <summary>osx-x64 runs under Rosetta on an arm64 Mac.</summary>
    public static bool NeedsRosetta(string rid) =>
        rid is "osx-x64"
        && OperatingSystem.IsMacOS()
        && RuntimeInformation.OSArchitecture == Architecture.Arm64;

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "rtok.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException(
            "rtok.slnx not found above " + AppContext.BaseDirectory
        );
    }

    [GeneratedRegex("^(osx|linux|linux-musl|win)-(x64|arm64)$", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex RidPattern();
}
