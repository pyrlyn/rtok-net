using Cake.Common.Diagnostics;
using Cake.Frosting;

namespace Rtok.Build;

/// <summary>
/// NativeAOT publish of rtok and rtok-hook for one RID into artifacts/dist/&lt;rid&gt;/ (plan/T3).
/// <c>--aot-tests</c> also publishes the AOT test lane (tests/Rtok.AotTests) next to them, so
/// RTOK_SMOKE_BIN can point at the directory. NativeAOT does not cross OSes: osx-* publishes on
/// macOS, win-* on Windows, linux-&lt;arch&gt; on Linux. linux-musl-* publishes inside the .NET
/// Alpine AOT SDK image (docker) on any Linux host of the same arch.
/// </summary>
[TaskName("publish")]
public sealed class PublishTask : AsyncFrostingTask<BuildContext>
{
    // The SDK image for musl builds; bump the tag and its digest together.
    private const string MuslImage =
        "mcr.microsoft.com/dotnet/sdk:10.0.402-alpine3.24-aot@sha256:4bafc89718cc5a590940ee8b0848a16bd54f28426bdd155ad54ee0e07843d9d8";

    public override async Task RunAsync(BuildContext context)
    {
        var rid = context.Rid("--target=publish --rid=<rid> [--aot-tests]");
        var outDir = "artifacts/dist/" + rid;
        List<string> projects = ["src/Rtok.Cli/Rtok.Cli.csproj", "src/Rtok.Hook/Rtok.Hook.csproj"];
        if (context.Flag("aot-tests"))
            projects.Add("tests/Rtok.AotTests/Rtok.AotTests.csproj");
        var revision = await context.RevisionAsync();

        var dist = Path.Combine(context.Root, outDir);
        if (Directory.Exists(dist))
            Directory.Delete(dist, recursive: true);
        // TreatWarningsAsErrors, not -warnaserror: EF's publish-time tasks warn without a code
        // ("NativeAOT support is experimental"), which -warnaserror would fail and no code can
        // exempt. Compiler and ILC warnings stay errors, minus EF's known ones
        // (src/Rtok.Store/StoreHost.targets).
        foreach (var project in projects)
        {
            string[] publish =
            [
                "publish",
                project,
                "-c",
                "Release",
                "-r",
                rid,
                "-p:TreatWarningsAsErrors=true",
                "-nologo",
                "-o",
                outDir,
                "-p:SourceRevisionId=" + revision,
            ];
            if (BuildContext.IsMusl(rid) && !BuildContext.OnAlpine)
                await context.RunAsync("docker", Container(context, ["dotnet", .. publish]));
            else
                await context.RunAsync("dotnet", publish);
        }
        if (BuildContext.IsMusl(rid) && !BuildContext.OnAlpine)
        {
            // The container wrote everything as root; hand it back to the host user.
            var uid = (await context.CaptureAsync("id", ["-u"])).StandardOutput.Trim();
            var gid = (await context.CaptureAsync("id", ["-g"])).StandardOutput.Trim();
            await context.RunAsync(
                "docker",
                Container(context, ["chown", "-R", $"{uid}:{gid}", "artifacts", "/nuget"])
            );
        }

        var ext = BuildContext.Exe(rid);
        foreach (var exe in new[] { "rtok", "rtok-hook" })
        {
            if (!File.Exists(Path.Combine(dist, exe + ext)))
                throw BuildContext.Fail($"publish produced no {outDir}/{exe}{ext}");
        }
        context.Information($"published {rid} to {outDir}");
    }

    /// <summary>
    /// <c>docker run</c> of one command in the musl SDK image, with bash for
    /// Pyrlyn.TreeSitter's build-native.sh; the NuGet cache is shared with the host.
    /// </summary>
    private static List<string> Container(BuildContext context, IEnumerable<string> command)
    {
        var nuget =
            Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget",
                "packages"
            );
        Directory.CreateDirectory(nuget);
        return
        [
            "run",
            "--rm",
            "-v",
            context.Root + ":/src",
            "-v",
            nuget + ":/nuget",
            "-w",
            "/src",
            "-e",
            "NUGET_PACKAGES=/nuget",
            "-e",
            "CI=" + Environment.GetEnvironmentVariable("CI"),
            "-e",
            "DOTNET_CLI_TELEMETRY_OPTOUT=1",
            MuslImage,
            "sh",
            "-c",
            "apk add --no-cache -q bash && exec \"$@\"",
            "sh",
            .. command,
        ];
    }
}
