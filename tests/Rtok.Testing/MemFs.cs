using System.IO.Abstractions;
using Testably.Abstractions.Testing;

namespace Rtok.Testing;

/// <summary>
/// The virtual file-system seam (rtok D29). Code that touches files takes an
/// <see cref="IFileSystem"/> (System.IO.Abstractions); unit tests hand it an in-memory
/// <see cref="MockFileSystem"/> instead of the host disk. The simulated OS gives Windows and
/// macOS path rules (drive letters, case-insensitive names) on any host.
/// </summary>
public static class MemFs
{
    /// <summary>An empty in-memory file system with the host's path rules.</summary>
    public static MockFileSystem Create() => new();

    /// <summary>Windows rules: <c>C:\</c> roots, backslashes, case-insensitive names.</summary>
    public static MockFileSystem Windows() => Simulating(SimulationMode.Windows);

    /// <summary>macOS rules: <c>/</c> root, case-insensitive names.</summary>
    public static MockFileSystem MacOS() => Simulating(SimulationMode.MacOS);

    /// <summary>Linux rules: <c>/</c> root, case-sensitive names.</summary>
    public static MockFileSystem Linux() => Simulating(SimulationMode.Linux);

    private static MockFileSystem Simulating(SimulationMode mode) =>
        new(o => o.SimulatingOperatingSystem(mode));
}
