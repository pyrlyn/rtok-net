using System.Text.RegularExpressions;
using Cake.Common.Diagnostics;
using Cake.Frosting;

namespace Rtok.Build;

/// <summary>
/// Test, pack and push one package for a &lt;Name&gt;/v&lt;version&gt; tag (packages/README.md).
/// <c>--dry-run</c> pushes to a local folder feed instead of NuGet. Otherwise NUGET_API_KEY is
/// required; NUGET_SOURCE defaults to nuget.org.
/// </summary>
[TaskName("publish-package")]
public sealed partial class PublishPackageTask : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        var tag = TagPattern().Match(context.Arg("tag"));
        if (!tag.Success)
        {
            throw BuildContext.Usage(
                "--target=publish-package --tag=<Name>/v<version> [--dry-run]"
            );
        }
        var name = tag.Groups["name"].Value;
        var version = tag.Groups["version"].Value;
        var dir = Path.Combine(context.Root, "packages", name);
        var project = Path.Combine(dir, "src", name + ".csproj");
        await CheckAsync(context, name, version, dir, project);

        var outDir = Path.Combine(context.Root, "artifacts/package", name, version);
        if (Directory.Exists(outDir))
            Directory.Delete(outDir, recursive: true);
        await context.RunAsync(
            "dotnet",
            ["test", "--solution", Path.Combine(dir, name + ".slnx"), "-c", "Release"]
        );
        await context.RunAsync(
            "dotnet",
            ["pack", project, "-c", "Release", "-warnaserror", "-o", outDir]
        );

        var nupkg = Path.Combine(outDir, $"{name}.{version}.nupkg");
        if (!File.Exists(nupkg))
            throw Fail(name, $"pack did not produce {name}.{version}.nupkg");
        // A package without an assembly (Pyrlyn.CodeStyle) sets IncludeSymbols=false and has no .snupkg.
        var symbols = await PropertyAsync(context, project, "IncludeSymbols");
        if (symbols is "true" && !File.Exists(Path.Combine(outDir, $"{name}.{version}.snupkg")))
            throw Fail(name, $"pack did not produce {name}.{version}.snupkg");

        if (context.Flag("dry-run"))
            await DryRunAsync(context, name, version, nupkg);
        else
            await PushAsync(context, name, nupkg);
    }

    /// <summary>The tag, the project version and the changelog agree; the README is finished.</summary>
    private static async Task CheckAsync(
        BuildContext context,
        string name,
        string version,
        string dir,
        string project
    )
    {
        if (!File.Exists(project))
            throw Fail(name, $"packages/{name}/src/{name}.csproj not found");
        var declared = await PropertyAsync(context, project, "Version");
        if (!string.Equals(declared, version, StringComparison.Ordinal))
            throw Fail(name, $"tag says {version}, the project says {declared}");
        var changelog = await File.ReadAllTextAsync(Path.Combine(dir, "CHANGELOG.md"));
        var entry = $@"^## \[?{Regex.Escape(version)}\]?( |\r?$)";
        var options = RegexOptions.Multiline | RegexOptions.ExplicitCapture;
        if (!Regex.IsMatch(changelog, entry, options, TimeSpan.FromSeconds(1)))
            throw Fail(name, $"CHANGELOG.md has no '## {version}' entry");
        var readme = await File.ReadAllTextAsync(Path.Combine(dir, "README.md"));
        if (readme.Contains("TODO", StringComparison.Ordinal))
            throw Fail(name, "README.md still has a TODO");
    }

    private static async Task DryRunAsync(
        BuildContext context,
        string name,
        string version,
        string nupkg
    )
    {
        var feed = Directory.CreateTempSubdirectory("rtok-feed-").FullName;
        try
        {
            await context.RunAsync("dotnet", ["nuget", "push", nupkg, "--source", feed]);
            if (!File.Exists(Path.Combine(feed, $"{name}.{version}.nupkg")))
                throw Fail(name, "dry-run push did not land in the local feed");
        }
        finally
        {
            Directory.Delete(feed, recursive: true);
        }
        context.Information($"{name} {version}: dry run ok");
    }

    private static async Task PushAsync(BuildContext context, string name, string nupkg)
    {
        var apiKey = Environment.GetEnvironmentVariable("NUGET_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
            throw Fail(name, "NUGET_API_KEY is not set");
        var source = Environment.GetEnvironmentVariable("NUGET_SOURCE") is { Length: > 0 } s
            ? s
            : "https://api.nuget.org/v3/index.json";
        // The .snupkg next to the .nupkg is pushed with it.
        await context.RunAsync(
            "dotnet",
            ["nuget", "push", nupkg, "--api-key", apiKey, "--source", source, "--skip-duplicate"]
        );
    }

    private static Cake.Core.CakeException Fail(string name, string message) =>
        BuildContext.Fail($"{name}: {message}");

    private static async Task<string> PropertyAsync(
        BuildContext context,
        string project,
        string property
    ) =>
        (
            await context.CaptureAsync(
                "dotnet",
                ["msbuild", project, "-getProperty:" + property, "-p:Configuration=Release"]
            )
        ).StandardOutput.Trim();

    [GeneratedRegex(
        @"^(?<name>(Pyrlyn|Rtok)(\.[A-Z][A-Za-z0-9]*)+)/v(?<version>[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?)$",
        RegexOptions.ExplicitCapture,
        1000
    )]
    private static partial Regex TagPattern();
}
