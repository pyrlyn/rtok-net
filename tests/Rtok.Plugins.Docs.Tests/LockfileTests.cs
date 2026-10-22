namespace Rtok.Plugins.Docs.Tests;

public sealed class LockfileTests : IDisposable
{
    private readonly DocsHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public void Cargo_lock_pins_the_first_package_of_that_exact_name() =>
        Assert.Equal(
            new PinnedPackage("serde", "1.0.210", Ecosystem.Cargo),
            CargoLock.Resolve(
                DocsHarness.CargoLockText
                    + "\n[[package]]\nname = \"serde\"\nversion = \"0.9.0\"\n",
                "serde"
            )
        );

    [Fact]
    public void Packages_lock_json_pins_an_id_in_any_case_under_its_own_spelling()
    {
        var pinned = NuGetLock.Resolve(DocsHarness.NuGetLockText, "newtonsoft.json");

        Assert.Equal(new PinnedPackage("Newtonsoft.Json", "13.0.3", Ecosystem.NuGet), pinned);
        Assert.Equal("nuget:newtonsoft.json", pinned?.StoreName);
    }

    [Fact]
    public void A_project_lockfile_below_the_workspace_is_found_and_build_output_is_not()
    {
        _h.Write(
            "obj/packages.lock.json",
            DocsHarness.NuGetLockText.Replace("13.0.3", "1.0.0", StringComparison.Ordinal)
        );
        _h.Write("src/App/packages.lock.json", DocsHarness.NuGetLockText);
        _h.Write("Cargo.lock", DocsHarness.CargoLockText);

        Assert.Equal("13.0.3", Lockfiles.Resolve(_h.Workspace, "Newtonsoft.Json")?.Version);
        Assert.Equal(Ecosystem.Cargo, Lockfiles.Resolve(_h.Workspace, "serde")?.Ecosystem);
    }

    [Theory]
    [InlineData("Cargo.lock", "[[package]\nname = ")]
    [InlineData("Cargo.lock", "package = 3")]
    [InlineData("Cargo.lock", "[[package]]\nname = \"serde\"\nversion = \"../../etc\"\n")]
    [InlineData("Cargo.lock", "[[package]]\nname = \"serde\"\n")]
    [InlineData("packages.lock.json", "{")]
    [InlineData("packages.lock.json", "[]")]
    [InlineData("packages.lock.json", """{"dependencies":{"net10.0":{"serde":{"resolved":7}}}}""")]
    [InlineData(
        "packages.lock.json",
        """{"dependencies":{"net10.0":{"serde":{"resolved":"1/../2"}}}}"""
    )]
    public void A_malformed_lockfile_pins_nothing(string file, string text)
    {
        _h.Write(file, text);

        Assert.Null(Lockfiles.Resolve(_h.Workspace, "serde"));
    }

    [Fact]
    public void A_workspace_without_a_lockfile_pins_nothing() =>
        Assert.Null(Lockfiles.Resolve(_h.Workspace, "serde"));
}
