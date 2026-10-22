using Cake.Frosting;

namespace Rtok.Build;

/// <summary>
/// Fuzzes one rtok-fuzz target under libFuzzer (plan/T53, tests/README.md). Publishes
/// tests/Rtok.Fuzz, instruments rtok's and Tomlyn's assemblies with sharpfuzz, builds
/// libfuzzer-dotnet from its pinned source with a clang that ships libFuzzer (CLANG=..., e.g.
/// Homebrew llvm on macOS; Apple clang has none) and runs it. New inputs land in
/// artifacts/fuzz/&lt;target&gt;/corpus, crashes in artifacts/fuzz/&lt;target&gt;/crash-*.
/// </summary>
[TaskName("fuzz")]
public sealed class FuzzTask : AsyncFrostingTask<BuildContext>
{
    // libfuzzer-dotnet v2025.05.02.0904; bump the commit to update.
    private const string LibFuzzerCommit = "bd39d4e88d715ab460a929943645be2a186cde52";

    public override async Task RunAsync(BuildContext context)
    {
        var target = context.Arg("fuzz-target");
        if (target is "")
        {
            throw BuildContext.Usage(
                "--target=fuzz --fuzz-target=<target> [--flags=\"<libFuzzer flags>\"]; targets: dotnet run --project tests/Rtok.Fuzz -- --list"
            );
        }
        var root = context.Root;
        var work = Path.Combine(root, "artifacts/fuzz");
        var outDir = Path.Combine(work, target);
        var seeds = Path.Combine(root, "tests/Rtok.Fuzz/corpus", target);
        if (!Directory.Exists(seeds))
            throw BuildContext.Fail($"no corpus for {target} at {seeds}");

        var harness = await HarnessAsync(context, work);
        var bin = await PublishAsync(context, outDir);

        var corpus = Path.Combine(outDir, "corpus");
        Directory.CreateDirectory(corpus);
        var flags = context.Arg("flags").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        await context.RunAsync(
            harness,
            [
                "--target_path=" + Path.Combine(bin, "rtok-fuzz"),
                "--target_arg=" + target,
                .. flags,
                corpus,
                seeds,
            ],
            workDir: outDir
        );
    }

    /// <summary>libfuzzer-dotnet, built once per pinned commit.</summary>
    private static async Task<string> HarnessAsync(BuildContext context, string work)
    {
        var harness = Path.Combine(work, "libfuzzer-dotnet-" + LibFuzzerCommit);
        if (File.Exists(harness))
            return harness;
        Directory.CreateDirectory(work);
        var source = Path.Combine(work, "libfuzzer-dotnet.cc");
        using (var http = new HttpClient())
        {
            var url = new Uri(
                $"https://raw.githubusercontent.com/Metalnem/libfuzzer-dotnet/{LibFuzzerCommit}/libfuzzer-dotnet.cc"
            );
            await File.WriteAllBytesAsync(source, await http.GetByteArrayAsync(url));
        }
        var clang = Environment.GetEnvironmentVariable("CLANG") is { Length: > 0 } c ? c : "clang";
        await context.RunAsync(clang, ["-fsanitize=fuzzer", source, "-o", harness]);
        return harness;
    }

    /// <summary>Publishes rtok-fuzz and instruments rtok's and Tomlyn's assemblies.</summary>
    private static async Task<string> PublishAsync(BuildContext context, string outDir)
    {
        var bin = Path.Combine(outDir, "bin");
        if (Directory.Exists(bin))
            Directory.Delete(bin, recursive: true);
        var project = Path.Combine(context.Root, "tests/Rtok.Fuzz");
        await context.RunAsync(
            "dotnet",
            ["publish", project, "-c", "Release", "-o", bin, "--nologo", "-v", "quiet"]
        );
        var manifest = Path.Combine(context.Root, ".config/dotnet-tools.json");
        await context.CaptureAsync("dotnet", ["tool", "restore", "--tool-manifest", manifest]);
        var assemblies = Directory
            .GetFiles(bin, "Pyrlyn.*.dll")
            .Concat(Directory.GetFiles(bin, "Rtok.*.dll"))
            .Append(Path.Combine(bin, "Tomlyn.dll"));
        foreach (var dll in assemblies)
            await context.RunAsync("dotnet", ["sharpfuzz", dll]);
        return bin;
    }
}
