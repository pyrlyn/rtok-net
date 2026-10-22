using Rtok.Mcp.Install;

namespace Rtok.Doctor.Tests;

public sealed class ExposureTests
{
    [Fact]
    public void Loopback_defaults_are_quiet()
    {
        var f = new DoctorFake();
        Assert.Null(f.Page().Exposure);
    }

    [Fact]
    public void A_wide_proxy_or_web_bind_is_reported()
    {
        var f = new DoctorFake();
        f.Config.Proxy.Enabled = true;
        f.Config.Proxy.Bind = "0.0.0.0";
        f.Config.Web.Host = "192.168.1.5";
        var lines = f.Page().Exposure!;
        Assert.Contains(
            lines,
            l => l.StartsWith("[proxy] bind = 0.0.0.0 is not loopback", StringComparison.Ordinal)
        );
        Assert.Contains(
            lines,
            l => l.StartsWith("[web] host = 192.168.1.5 is not loopback", StringComparison.Ordinal)
        );
        Assert.Contains("exposure\n  [proxy] bind", f.Text(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_wide_mcp_bind_needs_a_token_and_https()
    {
        var f = new DoctorFake();
        f.Config.Mcp.Http = "0.0.0.0:8791";
        var lines = f.Page().Exposure!;
        Assert.Contains(lines, l => l.Contains("no token is set", StringComparison.Ordinal));
        Assert.Contains(
            lines,
            l => l.Contains("public_url is not https", StringComparison.Ordinal)
        );

        f.Var("RTOK_MCP_TOKEN", "t0k3n");
        f.Config.Mcp.PublicUrl = "https://mcp.example.com";
        Assert.Null(f.Page().Exposure);
    }

    [Fact]
    public void Loose_modes_under_rtoks_home_are_reported_on_unix_only()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        var f = new DoctorFake();
        var db = f.At(".rtok/rtok.db");
        f.Write(db, "");
        f.Fs.File.SetUnixFileMode(
            db,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead
        );
        f.Fs.File.SetUnixFileMode(
            f.At(".rtok"),
            UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute
        );
        var secret = f.At(".rtok/key");
        f.Write(secret, "");
        f.Fs.File.SetUnixFileMode(secret, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        var lines = f.Page().Exposure!;

        Assert.Contains(
            lines,
            l =>
                l.StartsWith(
                    $"{f.At(".rtok")} is a directory open to others",
                    StringComparison.Ordinal
                )
        );
        Assert.Contains(
            lines,
            l =>
                l.StartsWith(db, StringComparison.Ordinal)
                && l.Contains("chmod 600", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(lines, l => l.StartsWith(secret, StringComparison.Ordinal));

        var windows = new DoctorFake(McpOs.Windows);
        windows.Write(windows.At(".rtok/rtok.db"), "");
        Assert.Null(windows.Page().Exposure);
    }
}
