using System.Text.RegularExpressions;
using Cake.Common.Diagnostics;
using Cake.Frosting;
using CliWrap;

namespace Rtok.Build;

/// <summary>
/// Smoke-tests one RID's rtok and rtok-hook on this runner (plan/T3): <c>rtok --version</c>
/// prints the version, and rtok-hook answers every hook fixture with exit 0 and <c>{}</c>.
/// <c>--dir</c> holds the binaries (artifacts/dist/&lt;rid&gt;, or an unpacked release archive).
/// osx-x64 runs under Rosetta on an arm64 Mac; linux-musl-* runs in an Alpine container.
/// </summary>
[TaskName("smoke")]
public sealed partial class SmokeTask : AsyncFrostingTask<BuildContext>
{
    // Plain Alpine for running musl binaries; bump the tag and its digest together.
    private const string AlpineImage =
        "alpine:3.22@sha256:822614ea121e1d40a33655081af10ef85f0aa2ab9e61f649ceaf0c651822433c";

    public override async Task RunAsync(BuildContext context)
    {
        const string usage = "--target=smoke --rid=<rid> --dir=<dir>";
        var rid = context.Rid(usage);
        var dir = context.PathArg("dir");
        if (dir is "" || !Directory.Exists(dir))
            throw BuildContext.Usage(usage);

        var version = (await RunAsync(context, rid, dir, "rtok", ["--version"])).TrimEnd(
            '\r',
            '\n'
        );
        if (!VersionPattern().IsMatch(version))
            throw BuildContext.Fail($"{rid}: rtok --version printed '{version}'");
        context.Information($"{rid}: {version}");

        var fixtures = Directory
            .GetFiles(Path.Combine(context.Root, "tests/fixtures/hooks"), "*.json")
            .Order(StringComparer.Ordinal)
            .ToList();
        foreach (var fixture in fixtures)
        {
            var reply = (await RunAsync(context, rid, dir, "rtok-hook", [], fixture)).TrimEnd(
                '\r',
                '\n'
            );
            if (reply is not "{}")
            {
                throw BuildContext.Fail(
                    $"{rid}: rtok-hook answered {Path.GetFileName(fixture)} with '{reply}'"
                );
            }
        }
        context.Information($"{rid}: rtok-hook ok on {fixtures.Count} fixtures");
    }

    /// <summary>Runs one of the binaries under test natively, under Rosetta or in Alpine.</summary>
    private static async Task<string> RunAsync(
        BuildContext context,
        string rid,
        string dir,
        string exe,
        string[] args,
        string? stdinFile = null
    )
    {
        var stdin = stdinFile is null ? null : PipeSource.FromFile(stdinFile);
        string tool;
        List<string> all;
        if (BuildContext.IsMusl(rid) && !BuildContext.OnAlpine)
        {
            tool = "docker";
            all =
            [
                "run",
                "--rm",
                "-i",
                "-v",
                dir + ":/bin-under-test:ro",
                AlpineImage,
                "/bin-under-test/" + exe,
                .. args,
            ];
        }
        else if (BuildContext.NeedsRosetta(rid))
        {
            tool = "arch";
            all = ["-x86_64", Path.Combine(dir, exe), .. args];
        }
        else
        {
            tool = Path.Combine(dir, exe + BuildContext.Exe(rid));
            all = [.. args];
        }
        return (await context.CaptureAsync(tool, all, stdin)).StandardOutput;
    }

    [GeneratedRegex(@"^rtok [0-9]+\.[0-9]+\.[0-9]+.* \([0-9a-f]{9}\)$", RegexOptions.None, 1000)]
    private static partial Regex VersionPattern();
}
