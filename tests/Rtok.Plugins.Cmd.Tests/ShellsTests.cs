namespace Rtok.Plugins.Cmd.Tests;

/// <summary>Shell choice, flags and quoting for <c>rtok run</c>.</summary>
public sealed class ShellsTests
{
    [Fact]
    public void Script_keeps_one_arg_as_a_shell_snippet()
    {
        Assert.Equal(
            "{ true && false\n} 2>&1",
            Shells.ScriptFor(ShellKind.Posix, ["true && false"])
        );
        Assert.Equal(
            "{ 'git' 'status'\n} 2>&1",
            Shells.ScriptFor(ShellKind.Posix, ["git", "status"])
        );
    }

    [Fact]
    public void Script_for_cmd_quotes_multi_argv()
    {
        Assert.Equal(
            "(echo \"hello world\") 2>&1",
            Shells.ScriptFor(ShellKind.Cmd, ["echo", "hello world"])
        );
        Assert.Equal("(echo hi) 2>&1", Shells.ScriptFor(ShellKind.Cmd, ["echo hi"]));
        Assert.Equal("\"say \"\"hi\"\"\"", Shells.CmdQuote("say \"hi\""));
    }

    [Fact]
    public void Script_for_powershell_merges_every_stream()
    {
        Assert.Equal(
            "& { 'echo' 'it''s' } *>&1",
            Shells.ScriptFor(ShellKind.PowerShell, ["echo", "it's"])
        );
    }

    [Fact]
    public void Resolve_prefers_config_then_env_then_host_default()
    {
        Assert.Equal(
            @"C:\shell\bash.exe",
            Shells.Resolve(@"C:\shell\bash.exe", "/bin/zsh", true, "cmd.exe")
        );
        Assert.Equal(
            "/bin/zsh",
            Shells.Resolve("", "/bin/zsh", true, @"C:\Windows\System32\cmd.exe")
        );
        Assert.Equal(
            @"C:\Windows\System32\cmd.exe",
            Shells.Resolve("", null, true, @"C:\Windows\System32\cmd.exe")
        );
        Assert.Equal("cmd.exe", Shells.Resolve("", null, true, null));
        Assert.Equal("/bin/sh", Shells.Resolve("", null, false, null));
        Assert.Equal("/bin/sh", Shells.Resolve("", "", false, null));
    }

    // `$SHELL=fish` (nu, xonsh) cannot run the `{ … } 2>&1` body; config still wins.
    [Theory]
    [InlineData("/opt/homebrew/bin/fish")]
    [InlineData("nu")]
    [InlineData("/usr/bin/xonsh")]
    [InlineData("fish")]
    public void Resolve_ignores_a_non_posix_env_shell(string shell)
    {
        Assert.Equal("/bin/sh", Shells.Resolve("", shell, false, null));
        Assert.Equal("cmd.exe", Shells.Resolve("", shell, true, null));
        Assert.Equal("/usr/bin/fish", Shells.Resolve("/usr/bin/fish", "/bin/zsh", false, null));
    }

    [Theory]
    [InlineData("/bin/bash")]
    [InlineData("/usr/local/bin/zsh")]
    [InlineData("dash")]
    [InlineData("ksh")]
    [InlineData(@"C:\msys64\usr\bin\BASH.EXE")]
    public void Resolve_keeps_a_posix_env_shell(string shell) =>
        Assert.Equal(shell, Shells.Resolve("", shell, false, null));

    [Fact]
    public void Kind_and_args_match_windows_hosts()
    {
        Assert.Equal(ShellKind.Posix, Shells.Kind("/bin/sh"));
        Assert.Equal(ShellKind.Cmd, Shells.Kind(@"C:\Windows\System32\cmd.exe"));
        Assert.Equal(ShellKind.PowerShell, Shells.Kind("powershell.exe"));
        Assert.Equal(ShellKind.PowerShell, Shells.Kind("pwsh"));
        Assert.Equal(["-c", "true"], Shells.Args("/bin/sh", "true"));
        Assert.Equal(
            ["/D", "/C", "echo hi"],
            Shells.Args(@"C:\Windows\System32\cmd.exe", "echo hi")
        );
        Assert.Equal(
            ["-NoProfile", "-NonInteractive", "-Command", "Get-Date"],
            Shells.Args("pwsh.exe", "Get-Date")
        );
    }

    // A configured PowerShell must not get POSIX `-c`.
    [Fact]
    public void Configured_powershell_uses_the_command_flag()
    {
        var shell = Shells.Resolve("powershell.exe", "/bin/zsh", false, null);
        var args = Shells.Args(shell, Shells.ScriptFor(Shells.Kind(shell), ["echo", "x"]));
        Assert.Equal("-NoProfile", args[0]);
        Assert.Equal("-Command", args[2]);
    }

    [Fact]
    public void Wrap_quote_is_host_safe_for_an_apostrophe()
    {
        Assert.Equal("'echo it''s'", Shells.WrapQuote("echo it's", windows: true));
        Assert.Equal("'echo it'\"'\"'s'", Shells.WrapQuote("echo it's", windows: false));
    }
}
