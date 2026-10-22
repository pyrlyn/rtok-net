using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Cake.Common.Diagnostics;
using Cake.Frosting;
using CliWrap;

namespace Rtok.Build;

/// <summary>
/// Packs one RID's published binaries into its release archive (plan/T3).
/// <c>--rtok</c> is an rtok this host can run; it writes the man pages and completions (they are
/// the same for every RID). <c>--allow-missing</c> packs what exists and warns about the rest
/// (CI); a release fails instead. Writes artifacts/release/rtok-&lt;rid&gt;.tar.gz (.zip for
/// win-*) and its .sha256. The archive holds one rtok-&lt;rid&gt;/ directory:
/// rtok and rtok-hook (.exe on Windows), share/man/man1/*.1 (rtok man),
/// share/completions/* (rtok completions &lt;shell&gt;), plugins/ skills/ rules/ modes/ from the
/// repo, and LICENSE.
/// </summary>
[TaskName("package-release")]
public sealed class PackageReleaseTask : AsyncFrostingTask<BuildContext>
{
    private static readonly (string Shell, string File)[] Completions =
    [
        ("bash", "rtok.bash"),
        ("zsh", "_rtok"),
        ("fish", "rtok.fish"),
        ("powershell", "rtok.ps1"),
        ("clink", "rtok.lua"),
    ];

    public override async Task RunAsync(BuildContext context)
    {
        const string usage =
            "--target=package-release --rid=<rid> --rtok=<native rtok> [--allow-missing]";
        var rid = context.Rid(usage);
        var rtok = context.PathArg("rtok");
        if (rtok is "")
            throw BuildContext.Usage(usage);
        // CI names the native rtok without .exe on every OS.
        if (OperatingSystem.IsWindows() && !File.Exists(rtok) && File.Exists(rtok + ".exe"))
            rtok += ".exe";
        var allowMissing = context.Flag("allow-missing");
        void Missing(string what)
        {
            if (!allowMissing)
                throw BuildContext.Fail($"error: {rid} archive needs {what}");
            context.Warning($"warning: {rid} archive has no {what}");
        }

        var name = "rtok-" + rid;
        var release = Path.Combine(context.Root, "artifacts/release");
        var stage = Path.Combine(release, "stage", name);
        if (Directory.Exists(stage))
            Directory.Delete(stage, recursive: true);
        Directory.CreateDirectory(stage);
        StageBinaries(context.Root, rid, stage);
        await StageDocsAsync(context, rtok, stage, Missing);
        foreach (var dir in new[] { "plugins", "skills", "rules", "modes" })
        {
            var source = Path.Combine(context.Root, dir);
            if (Directory.Exists(source))
                CopyTree(source, Path.Combine(stage, dir));
            else
                Missing(dir + "/");
        }

        var archive = name + (BuildContext.Exe(rid) is "" ? ".tar.gz" : ".zip");
        await ArchiveAsync(stage, Path.Combine(release, archive));
        context.Information($"packed artifacts/release/{archive}");
    }

    private static void StageBinaries(string root, string rid, string stage)
    {
        var ext = BuildContext.Exe(rid);
        foreach (var exe in new[] { "rtok", "rtok-hook" })
        {
            var bin = Path.Combine(root, "artifacts/dist", rid, exe + ext);
            if (!File.Exists(bin))
            {
                throw BuildContext.Fail(
                    $"error: artifacts/dist/{rid}/{exe}{ext} not found; run --target=publish --rid={rid}"
                );
            }
            File.Copy(bin, Path.Combine(stage, exe + ext));
        }
        File.Copy(Path.Combine(root, "LICENSE"), Path.Combine(stage, "LICENSE"));
    }

    /// <summary>Man pages and completions come from the CLI's own command model (T12.11).</summary>
    private static async Task StageDocsAsync(
        BuildContext context,
        string rtok,
        string stage,
        Action<string> missing
    )
    {
        var help = await context.CaptureAsync(rtok, ["--help"], allowFailure: true);
        var commands = help.StandardOutput + help.StandardError;
        if (HasCommand(commands, "man"))
        {
            var man = Path.Combine(stage, "share/man/man1");
            Directory.CreateDirectory(man);
            await context.RunAsync(rtok, ["man", "--dir", man]);
            if (Directory.GetFiles(man, "*.1").Length == 0)
                missing("man pages (rtok man wrote none)");
        }
        else
        {
            missing("share/man/man1 (rtok has no man command yet)");
        }
        if (!HasCommand(commands, "completions"))
        {
            missing("share/completions (rtok has no completions command yet)");
            return;
        }
        var completions = Path.Combine(stage, "share/completions");
        Directory.CreateDirectory(completions);
        foreach (var (shell, file) in Completions)
        {
            await Cli.Wrap(rtok)
                .WithArguments(["completions", shell])
                .WithStandardOutputPipe(PipeTarget.ToFile(Path.Combine(completions, file)))
                .ExecuteAsync();
        }
    }

    /// <summary>A .tar.gz, or a .zip by name, and its .sha256 in the sha256sum format.</summary>
    private static async Task ArchiveAsync(string stage, string archive)
    {
        File.Delete(archive);
        if (archive.EndsWith(".zip", StringComparison.Ordinal))
        {
            ZipFile.CreateFromDirectory(
                stage,
                archive,
                CompressionLevel.Optimal,
                includeBaseDirectory: true
            );
        }
        else
        {
            // TarFile keeps each file's Unix mode, so the binaries stay executable.
            await using var file = File.Create(archive);
            await using var gzip = new GZipStream(file, CompressionLevel.Optimal);
            await TarFile.CreateFromDirectoryAsync(stage, gzip, includeBaseDirectory: true);
        }
        await using var read = File.OpenRead(archive);
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(read));
        await File.WriteAllTextAsync(archive + ".sha256", $"{hash}  {Path.GetFileName(archive)}\n");
    }

    private static bool HasCommand(string help, string command) =>
        Regex.IsMatch(
            help,
            $"^ +{Regex.Escape(command)}( |\r?$)",
            RegexOptions.Multiline | RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1)
        );

    /// <summary>Copies a directory tree; File.Copy keeps the Unix mode (hook scripts stay executable).</summary>
    private static void CopyTree(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, dir)));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
    }
}
