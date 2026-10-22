namespace Rtok.Measure.Tests;

public sealed class BashFamilyTests
{
    [Theory]
    [InlineData("cd /tmp && git status", "git")]
    [InlineData("FOO=1 grep x", "grep")]
    [InlineData("sed -n 1p", "sed")]
    [InlineData(@"C:\Git\cmd\git.exe status", "git")]
    [InlineData("cargo.exe test", "cargo")]
    [InlineData("", "other")]
    public void Strips_cd_env_folder_and_exe(string cmd, string family) =>
        Assert.Equal(family, BashFamily.Of(cmd));

    [Theory]
    [InlineData("cd 'My Documents' && git status", "git")]
    [InlineData("cd \"C:\\Program Files\\App\" && npm test", "npm")]
    [InlineData("cd ~/'My Documents'/src && cargo build", "cargo")]
    [InlineData("FOO=\"a b\" cd 'x y' && rg z", "rg")]
    [InlineData("cd\t'a b'\t&& sed -n 1p", "sed")]
    public void Strips_quoted_cd_paths(string cmd, string family) =>
        Assert.Equal(family, BashFamily.Of(cmd));

    /// <summary>Malformed quotes fail open, and <c>cd</c> glued to more is not <c>cd</c>.</summary>
    [Theory]
    [InlineData("cd 'unterminated && git status", "cd")]
    [InlineData("FOO='x cd y && git status", "FOO='x")]
    [InlineData("cdx && git status", "cdx")]
    public void Leaves_malformed_prefixes_alone(string cmd, string family) =>
        Assert.Equal(family, BashFamily.Of(cmd));

    [Fact]
    public void Asks_the_visible_program_seam_first() =>
        Assert.Equal(
            "cargo",
            BashFamily.Of(
                "mise exec -- cargo test",
                w => w is ["mise", "exec", "--", var p, ..] ? p : null
            )
        );
}
