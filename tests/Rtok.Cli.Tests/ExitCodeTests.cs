namespace Rtok.Cli.Tests;

public sealed class ExitCodeTests
{
    [Theory]
    [InlineData(
        "config set proxy.port",
        "Error: Required argument 'value'",
        "Run 'rtok config set --help' for usage."
    )]
    [InlineData(
        "config get a b",
        "Error: Argument 'b' is not recognized",
        "Run 'rtok config get --help' for usage."
    )]
    [InlineData("config show --bogus", "Error: ", "Run 'rtok config show --help' for usage.")]
    [InlineData("plugins --json maybe", "Error: ", "Run 'rtok plugins --help' for usage.")]
    [InlineData("bogus", "Error: unknown command 'bogus'", "Run 'rtok --help' for usage.")]
    [InlineData(
        "config bogus",
        "Error: unknown command 'bogus'",
        "Run 'rtok config --help' for usage."
    )]
    [InlineData("--bogus", "Error: unexpected option '--bogus'", "Run 'rtok --help' for usage.")]
    [InlineData(
        "config --bogus",
        "Error: unexpected option '--bogus'",
        "Run 'rtok config --help' for usage."
    )]
    [InlineData(
        "completions tcsh",
        "Error: unknown shell 'tcsh'",
        "Run 'rtok completions --help' for usage."
    )]
    [InlineData("completions bash --install --uninstall", "Error: --install and --uninstall", "")]
    public void Bad_arguments_exit_2_with_one_error_line_and_a_hint(
        string args,
        string error,
        string hint
    )
    {
        var (code, stdout, stderr) = RtokBinary.Exec(args.Split(' '));

        Assert.Equal((2, ""), (code, stdout));
        var lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith(error, lines[0], StringComparison.Ordinal);
        Assert.StartsWith(hint, lines[1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("config")]
    [InlineData("--help")]
    [InlineData("bogus --help")]
    [InlineData("config show --help")]
    [InlineData("--version")]
    public void Help_and_a_bare_group_exit_0(string args)
    {
        var (code, stdout, stderr) = RtokBinary.Exec(
            args.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        );

        Assert.Equal((0, ""), (code, stderr));
        Assert.NotEmpty(stdout);
    }

    [Fact]
    public void A_command_that_fails_exits_1_with_one_error_line()
    {
        var (code, stdout, stderr) = RtokBinary.Exec(
            ["completions", "--install"],
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["SHELL"] = null }
        );

        Assert.Equal((1, ""), (code, stdout));
        Assert.Equal(
            "Error: cannot tell the shell from $SHELL; name one of: bash, zsh, fish, powershell, clink\n",
            stderr.ReplaceLineEndings("\n")
        );
    }

    [Fact]
    public void A_command_keeps_its_own_exit_code()
    {
        using var dir = new Rtok.Testing.TempDir();
        var bad = dir.Write("bad.toml", "[proxy]\nport = \"x\"\n");

        Assert.Equal(1, RtokBinary.Exec(["config", "validate", bad]).Code);
    }
}
