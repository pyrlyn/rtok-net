using System.Text;
using System.Xml.Linq;
using Cake.Common.Diagnostics;
using Cake.Frosting;

namespace Rtok.Build;

/// <summary>
/// Writes a solution filter with the JIT test projects a change touches (plan/T3): the files
/// <c>git diff --name-only &lt;base&gt;...HEAD</c> names, or every project with <c>--all</c>.
/// A changed file belongs to the project whose directory holds it; every project that
/// references it, directly or not, is affected too. A change outside any project (build props,
/// global.json, eng/ files, .github/, test fixtures) runs everything; docs and the plan run
/// nothing. Prints the chosen projects; the filter lists none when nothing is affected.
/// </summary>
[TaskName("affected-tests")]
public sealed class AffectedTestsTask : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        var outFile = context.PathArg("out");
        var baseRef = context.Arg("base");
        var all = context.Flag("all");
        if (outFile is "" || (baseRef is "") != all)
        {
            throw BuildContext.Usage(
                "--target=affected-tests --out=<file.slnf> (--base=<git ref> | --all)"
            );
        }
        // The filter names the solution relative to itself; the filter must sit inside the repo.
        var inside = Path.GetRelativePath(context.Root, Path.GetDirectoryName(outFile)!);
        if (inside.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(inside))
            throw BuildContext.Usage($"{outFile} must be inside {context.Root}");

        var projects = Projects(context.Root);
        var affected = all ? null : await ChangedAsync(context, projects, baseRef);
        if (affected is null)
        {
            affected = new HashSet<string>(projects, StringComparer.Ordinal);
        }
        else
        {
            // Close over reverse references until nothing new is added.
            var edges = References(context.Root, projects);
            for (var grew = true; grew; )
            {
                grew = false;
                foreach (var (from, to) in edges)
                    grew |= affected.Contains(to) && affected.Add(from);
            }
        }

        var tests = projects
            .Where(p =>
                p.EndsWith(".Tests.csproj", StringComparison.Ordinal) && affected.Contains(p)
            )
            .ToList();
        await WriteFilterAsync(context.Root, outFile, tests);
        context.Information($"{tests.Count} test project(s):");
        foreach (var test in tests)
            context.Information("  " + test);
        if (Environment.GetEnvironmentVariable("GITHUB_OUTPUT") is { Length: > 0 } output)
            await File.AppendAllTextAsync(output, $"count={tests.Count}\n");
    }

    /// <summary>Every project in the solution, as repo-relative paths.</summary>
    private static List<string> Projects(string root) =>
        XDocument
            .Load(Path.Combine(root, "rtok.slnx"))
            .Descendants("Project")
            .Select(p => (string?)p.Attribute("Path"))
            .OfType<string>()
            .Where(p => p.EndsWith(".csproj", StringComparison.Ordinal))
            .ToList();

    /// <summary>Project references as (referencing, referenced) repo-relative paths.</summary>
    private static List<(string From, string To)> References(string root, List<string> projects) =>
        projects
            .SelectMany(p =>
                XDocument
                    .Load(Path.Combine(root, p))
                    .Descendants("ProjectReference")
                    .Select(r => (string?)r.Attribute("Include"))
                    .OfType<string>()
                    .Select(r =>
                        (
                            From: p,
                            To: Path.GetRelativePath(
                                    root,
                                    Path.GetFullPath(
                                        Path.Combine(root, Dir(p), r.Replace('\\', '/'))
                                    )
                                )
                                .Replace('\\', '/')
                        )
                    )
            )
            .ToList();

    /// <summary>The projects the diff touches directly, or null when it needs a full run.</summary>
    private static async Task<HashSet<string>?> ChangedAsync(
        BuildContext context,
        List<string> projects,
        string baseRef
    )
    {
        var diff = await context.CaptureAsync("git", ["diff", "--name-only", baseRef + "...HEAD"]);
        var affected = new HashSet<string>(StringComparer.Ordinal);
        var files = diff.StandardOutput.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        foreach (var file in files)
        {
            var owner = projects
                .Select(p => Dir(p) + "/")
                .Where(d => file.StartsWith(d, StringComparison.Ordinal))
                .MaxBy(d => d.Length);
            if (owner != null)
            {
                affected.UnionWith(
                    projects.Where(p =>
                        string.Equals(Dir(p) + "/", owner, StringComparison.Ordinal)
                    )
                );
            }
            else if (!RunsNothing(file))
            {
                context.Information($"full run: {file} is outside every project");
                return null;
            }
        }
        return affected;
    }

    private static async Task WriteFilterAsync(string root, string outFile, List<string> tests)
    {
        var outDir = Path.GetDirectoryName(outFile)!;
        Directory.CreateDirectory(outDir);
        var slnx = Path.GetRelativePath(outDir, Path.Combine(root, "rtok.slnx")).Replace('\\', '/');
        var json = new StringBuilder()
            .Append("{\n  \"solution\": {\n    \"path\": \"")
            .Append(slnx)
            .Append("\",\n    \"projects\": [")
            .AppendJoin(",", tests.Select(t => "\n      \"" + t + "\""))
            .Append("\n    ]\n  }\n}\n");
        await File.WriteAllTextAsync(outFile, json.ToString());
    }

    private static string Dir(string project) => Path.GetDirectoryName(project)!.Replace('\\', '/');

    private static bool RunsNothing(string file) =>
        file.EndsWith(".md", StringComparison.Ordinal)
        || file.StartsWith("plan/", StringComparison.Ordinal)
        || file.StartsWith("docs/", StringComparison.Ordinal)
        || file.StartsWith("spikes/", StringComparison.Ordinal)
        || file is "LICENSE";
}
