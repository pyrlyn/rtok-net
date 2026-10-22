namespace Rtok.Plugins.Docs;

/// <summary>The package ecosystem a lockfile pins for.</summary>
public enum Ecosystem
{
    /// <summary>Rust crates, from <c>Cargo.lock</c>; docs from docs.rs rustdoc JSON.</summary>
    Cargo,

    /// <summary>NuGet packages, from <c>packages.lock.json</c>; docs from the package's XML docs.</summary>
    NuGet,
}
