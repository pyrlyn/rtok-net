using System.Text;
using Rtok.Process;

namespace Rtok.Plugins.Graph.Tests;

/// <summary>git in a test directory, with a fixed author and no user configuration.</summary>
internal static class TestGit
{
    public static string Run(string dir, params string[] args)
    {
        var result = ProgressRunner
            .Quiet("git", ["-C", dir, "-c", "user.name=t", "-c", "user.email=t@t", .. args])
            .Environment("GIT_CONFIG_GLOBAL", "/dev/null")
            .Environment("GIT_CONFIG_NOSYSTEM", "1")
            .Timeout(TimeSpan.FromSeconds(30))
            .RunAsync(TestContext.Current.CancellationToken)
            .GetAwaiter()
            .GetResult();
        Assert.True(
            result.ExitCode == 0,
            $"git {string.Join(' ', args)}: {Encoding.UTF8.GetString(result.Stderr)}"
        );
        return Encoding.UTF8.GetString(result.Stdout).Trim();
    }

    /// <summary>Writes <paramref name="files"/> with <paramref name="message"/> as content and
    /// commits only them.</summary>
    public static void Commit(GraphHarness h, string message, params string[] files)
    {
        foreach (var file in files)
        {
            h.Write(file, message + "\n");
        }

        Run(h.Project.Path, ["add", "--", .. files]);
        Run(h.Project.Path, "commit", "-q", "-m", message);
    }

    public static void Init(GraphHarness h) => Run(h.Project.Path, "init", "-q", "-b", "main");
}
