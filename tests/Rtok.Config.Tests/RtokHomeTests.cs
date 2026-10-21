using Rtok.Hook.Contracts;

namespace Rtok.Config.Tests;

public sealed class RtokHomeTests
{
    private static readonly Func<string?> Cwd = () => "/work";
    private static readonly Func<string> Temp = () => "/tmp/.rtok";

    [Theory]
    [InlineData("/r", "/u", "/r")]
    [InlineData(null, "/u", "/u/.rtok")]
    [InlineData("", "/u", "/u/.rtok")]
    [InlineData("~/h", "/u", "/u/h")]
    [InlineData("rel", "/u", "/work/rel")]
    [InlineData(null, "home", "/work/home/.rtok")]
    [InlineData(null, null, "/tmp/.rtok")]
    public void The_home_is_always_absolute(string? rtokHome, string? userHome, string expected) =>
        Assert.Equal(expected, RtokHome.Absolute(rtokHome, userHome, Cwd, Temp));

    [Fact]
    public void The_user_home_is_HOME_else_USERPROFILE_and_empty_does_not_count()
    {
        Assert.Equal(
            "/a",
            RtokHome.UserHome(n => string.Equals(n, "HOME", StringComparison.Ordinal) ? "/a" : "/b")
        );
        Assert.Equal(
            "/b",
            RtokHome.UserHome(n => string.Equals(n, "HOME", StringComparison.Ordinal) ? "" : "/b")
        );
        Assert.Null(RtokHome.UserHome(_ => null));
    }

    [Fact]
    public void The_user_file_is_the_flag_else_RTOK_CONFIG_else_config_toml_in_the_home()
    {
        Assert.Equal("/f.toml", RtokHome.UserConfigFile("/h", "/f.toml", _ => "/e.toml"));
        Assert.Equal("/e.toml", RtokHome.UserConfigFile("/h", null, _ => "/e.toml"));
        Assert.Equal(
            Path.Join("/h", "config.toml"),
            RtokHome.UserConfigFile("/h", null, _ => null)
        );
    }

    [Theory]
    [InlineData("~/.rtok/db", "/r/db")]
    [InlineData("~/.rtok", "/r")]
    [InlineData("~/x/y", "/u/x/y")]
    [InlineData("~", "/u")]
    [InlineData("/abs/~", "/abs/~")]
    [InlineData("rel", "rel")]
    public void A_tilde_follows_the_rtok_home_under_dot_rtok_and_the_user_home_elsewhere(
        string path,
        string expected
    ) => Assert.Equal(expected, RtokHome.Expand(path, "/r", "/u"));
}
