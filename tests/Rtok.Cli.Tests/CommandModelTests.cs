using Pyrlyn.CommandLine.Docs;
using Rtok.Cli.Shell;
using Rtok.Testing;

namespace Rtok.Cli.Tests;

/// <summary>
/// The committed command model against the binary's own <c>--help</c>, and the goldens drawn
/// from it. With <c>RTOK_UPDATE_GOLDENS=1</c> the drift test rewrites <c>docs/cli/commands.json</c>.
/// </summary>
public sealed class CommandModelTests
{
    private static readonly string Committed = Path.Join(
        RepoPaths.Root,
        "docs",
        "cli",
        "commands.json"
    );

    [Fact]
    public void The_committed_model_matches_the_binary()
    {
        if (Environment.GetEnvironmentVariable("RTOK_UPDATE_GOLDENS") is "1")
        {
            File.WriteAllText(Committed, CommandModelJson.Serialize(RtokBinary.Model));
            return;
        }
        Assert.Null(CommandModelJson.Diff(File.ReadAllText(Committed), RtokBinary.Model));
    }

    [Fact]
    public void The_binary_embeds_the_committed_model() =>
        Assert.Equal(File.ReadAllText(Committed), CommandModelJson.Serialize(CommandDocs.Model));

    [Fact]
    public Task Every_help_page() =>
        Verify(
            string.Concat(
                RtokBinary.Model.Commands.Select(c =>
                {
                    string[] words = [.. Words(c.Path), "--help"];
                    return $"$ rtok {string.Join(' ', words)}\n{RtokBinary.Run(words)}\n";
                })
            )
        );

    public static TheoryData<string> Shells => [.. CommandDocs.Shells];

    [Theory]
    [MemberData(nameof(Shells))]
    public Task Each_shell_has_its_completion_script(string shell) =>
        Verify(Printed(["completions", shell])).UseParameters(shell);

    [Fact]
    public Task The_man_page_is_roff() => Verify(Printed(["man"]));

    [Fact]
    public void Man_dir_writes_a_page_per_command_and_prints_the_paths()
    {
        using var dir = new TempDir();

        var (code, stdout, _) = RtokBinary.Exec(["man", "--dir", dir.Path]);

        Assert.Equal(0, code);
        var names = ManPages
            .Render(CommandDocs.Model)
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal);
        Assert.Equal(
            names,
            Directory.GetFiles(dir.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal)
        );
        Assert.Equal(
            names.Count(),
            stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length
        );
    }

    private static string Printed(string[] args)
    {
        var (code, stdout, stderr) = RtokBinary.Exec(args);
        Assert.True(code == 0, stderr);
        return stdout;
    }

    private static string[] Words(string path) =>
        path.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
