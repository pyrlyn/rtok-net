using Rtok.Cli.Shell;
using Rtok.Testing;

namespace Rtok.Cli.Tests;

public sealed class CompletionInstallerTests
{
    [Theory]
    [InlineData("bash", ".local/share/bash-completion/completions/rtok")]
    [InlineData("zsh", ".zfunc/_rtok")]
    [InlineData("fish", ".config/fish/completions/rtok.fish")]
    [InlineData("powershell", ".config/powershell/rtok.ps1")]
    [InlineData("clink", "AppData/Local/clink/rtok.lua")]
    public void Each_shell_lands_in_its_standard_place_and_reinstall_is_unchanged(
        string shell,
        string relative
    )
    {
        using var home = new TempDir();
        var places = new CompletionPlaces
        {
            Home = home.Path,
            LocalAppData = home.Combine("AppData", "Local"),
        };
        var path = home.Combine(relative.Split('/'));

        Assert.Equal(
            $"wrote {path}",
            CompletionInstaller.Install(shell, "script\n", places)[0].ToString()
        );
        Assert.Equal("script\n", File.ReadAllText(path));
        Assert.Equal(
            $"unchanged {path}",
            CompletionInstaller.Install(shell, "script\n", places)[0].ToString()
        );
        Assert.Equal($"removed {path}", CompletionInstaller.Uninstall(shell, places)[0].ToString());
        Assert.False(File.Exists(path));
        Assert.Equal($"absent {path}", CompletionInstaller.Uninstall(shell, places)[0].ToString());
    }

    [Fact]
    public void Xdg_dirs_win_over_the_home_defaults_and_zsh_says_how_it_loads()
    {
        using var home = new TempDir();
        var places = new CompletionPlaces
        {
            Home = home.Path,
            XdgData = home.Combine("data"),
            ZDotDir = home.Combine("z"),
        };

        Assert.Equal(
            home.Combine("data", "bash-completion", "completions", "rtok"),
            places.Script("bash").Path
        );
        var zsh = CompletionInstaller.Install("zsh", "x", places);
        Assert.Equal(
            $"zsh loads it when .zshrc has: fpath+=({home.Combine("z", ".zfunc")}); autoload -Uz compinit && compinit",
            zsh[^1].ToString()
        );
    }

    [Fact]
    public void The_powershell_profile_keeps_everything_else_byte_for_byte()
    {
        using var home = new TempDir();
        var places = new CompletionPlaces { Home = home.Path };
        var mine = "Set-Alias ll ls\r\n# keep me";
        home.Write(".config/powershell/Microsoft.PowerShell_profile.ps1", mine);

        CompletionInstaller.Install("powershell", "x", places);
        var with = File.ReadAllText(places.PowerShellProfile);
        Assert.StartsWith($"{mine}\n. \"", with, StringComparison.Ordinal);
        Assert.Equal(
            $"unchanged {places.PowerShellProfile}",
            CompletionInstaller.Install("powershell", "x", places)[1].ToString()
        );

        Assert.Equal(
            $"updated {places.PowerShellProfile}",
            CompletionInstaller.Uninstall("powershell", places)[1].ToString()
        );
        Assert.Equal($"{mine}\n", File.ReadAllText(places.PowerShellProfile));
    }

    [Fact]
    public void The_shell_comes_from_dollar_shell_or_the_error_lists_them()
    {
        var places = new CompletionPlaces { Home = "/h", Shell = "/usr/local/bin/fish" };

        Assert.Equal("fish", places.Pick(null));
        Assert.Equal("zsh", places.Pick("zsh"));
        var error = Assert.Throws<InvalidOperationException>(() =>
            (places with { Shell = null }).Pick(null)
        );
        Assert.Contains(
            "bash, zsh, fish, powershell, clink",
            error.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Clink_without_local_app_data_is_refused()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            new CompletionPlaces { Home = "/h" }.Script("clink")
        );

        Assert.Contains("LOCALAPPDATA", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Windows_keeps_the_powershell_profile_under_documents()
    {
        var places = new CompletionPlaces { Home = "/h", Windows = true };

        Assert.Equal(
            Path.Join("/h", "Documents", "PowerShell", "Microsoft.PowerShell_profile.ps1"),
            places.PowerShellProfile
        );
    }
}
