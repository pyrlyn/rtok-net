using System.Text.Json;

namespace Rtok.Cli.Tests;

/// <summary>
/// Every command that reads state takes <c>--json</c>; every other one says why not. A new
/// command fails here until it is put in one of the two lists.
/// </summary>
public sealed class JsonTests
{
    private static readonly Dictionary<string, string[]> Reading = new(StringComparer.Ordinal)
    {
        ["agents usage"] = ["--tz", "UTC"],
        ["agents list"] = [],
        ["agents info"] = ["claude"],
        ["config show"] = [],
        ["config get"] = ["proxy.port"],
        ["plugins"] = [],
        ["logs"] = [],
        ["logs export"] = [],
    };

    private static readonly Dictionary<string, string> Other = new(StringComparer.Ordinal)
    {
        ["config init"] = "writes the reference file",
        ["config set"] = "writes a key",
        ["config path"] = "prints one path, for $(rtok config path)",
        ["config validate"] = "its exit code is the answer",
        ["docs fetch"] = "downloads docs and prints one status line",
        ["hook"] = "speaks the host's hook protocol",
        ["completions"] = "prints or installs a shell script",
        ["man"] = "prints or writes roff pages",
        ["logs watch"] = "streams the log until Ctrl-C",
        ["agents install"] = "writes host files",
        ["agents update"] = "writes host files",
        ["agents uninstall"] = "writes host files",
        ["run"] = "prints the command's own output, filtered",
        ["filter"] = "prints its stdin, filtered",
    };

    [Fact]
    public void Every_command_is_either_reading_or_says_why_not()
    {
        var commands = RtokBinary.Model.Commands.Where(c => c.Path.Length > 0).Select(c => c.Path);

        Assert.Equal(
            commands.Order(StringComparer.Ordinal),
            Reading.Keys.Concat(Other.Keys).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void Every_reading_command_prints_json_with_json()
    {
        foreach (var (path, args) in Reading)
        {
            var command = RtokBinary.Model.Commands.Single(c =>
                string.Equals(c.Path, path, StringComparison.Ordinal)
            );
            Assert.Contains(
                command.Options,
                o => o.Names.Contains("--json", StringComparer.Ordinal)
            );

            var (code, stdout, stderr) = RtokBinary.Exec([.. path.Split(' '), .. args, "--json"]);

            Assert.True(code == 0, $"{path}: {stderr}");
            using var _ = JsonDocument.Parse(stdout);
        }
    }
}
