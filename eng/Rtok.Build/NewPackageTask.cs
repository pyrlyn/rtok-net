using System.Text.RegularExpressions;
using Cake.Common.Diagnostics;
using Cake.Frosting;

namespace Rtok.Build;

/// <summary>Scaffolds packages/&lt;Name&gt;/ in the shape packages/README.md describes and adds it to rtok.slnx.</summary>
[TaskName("new-package")]
public sealed partial class NewPackageTask : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        var name = context.Arg("name");
        var summary = context.Arg("summary");
        if (!NamePattern().IsMatch(name) || summary is "")
            throw BuildContext.Usage(
                "--target=new-package --name=<Pyrlyn.Name|Rtok.Name> --summary=\"<one line>\""
            );
        var dir = Path.Combine(context.Root, "packages", name);
        if (Path.Exists(dir))
            throw BuildContext.Fail($"packages/{name} already exists");

        Directory.CreateDirectory(Path.Combine(dir, "src"));
        Directory.CreateDirectory(Path.Combine(dir, "tests"));
        File.Copy(Path.Combine(context.Root, "LICENSE"), Path.Combine(dir, "LICENSE"));
        foreach (var (file, text) in Files(name, summary))
        {
            // "\n" line ends: the files are the same on every OS.
            File.WriteAllText(Path.Combine(dir, file), text.ReplaceLineEndings("\n") + "\n");
        }

        await context.CaptureAsync(
            "dotnet",
            [
                "sln",
                Path.Combine(context.Root, "rtok.slnx"),
                "add",
                "--solution-folder",
                "packages/" + name,
                Path.Combine(dir, "src", name + ".csproj"),
                Path.Combine(dir, "tests", name + ".Tests.csproj"),
            ]
        );
        context.Information($"created packages/{name}");
    }

    private static (string File, string Text)[] Files(string name, string summary) =>
        [.. Projects(name, summary), .. Docs(name, summary)];

    private static (string File, string Text)[] Projects(string name, string summary) =>
        [
            (
                $"src/{name}.csproj",
                $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <Version>0.1.0</Version>
                    <Description>{System.Security.SecurityElement.Escape(summary)}</Description>
                    <PackageTags></PackageTags>
                  </PropertyGroup>
                </Project>
                """
            ),
            (
                $"tests/{name}.Tests.csproj",
                $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <ProjectReference Include="../src/{name}.csproj" />
                  </ItemGroup>
                </Project>
                """
            ),
            (
                $"{name}.slnx",
                $"""
                <Solution>
                  <Project Path="src/{name}.csproj" />
                  <Project Path="tests/{name}.Tests.csproj" />
                </Solution>
                """
            ),
        ];

    private static (string File, string Text)[] Docs(string name, string summary) =>
        [
            (
                "README.md",
                $"""
                # {name}

                {summary}

                ## Install

                ```bash
                dotnet add package {name}
                ```

                ## Use

                TODO: a short example.
                """
            ),
            (
                "CHANGELOG.md",
                """
                # Changelog

                ## 0.1.0

                - First release.
                """
            ),
        ];

    [GeneratedRegex(@"^(Pyrlyn|Rtok)(\.[A-Z][A-Za-z0-9]*)+$", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex NamePattern();
}
