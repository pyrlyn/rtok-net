using System.Runtime.InteropServices;

namespace Pyrlyn.FsKit.Tests;

/// <summary>A fresh directory under the temp dir, symlink-free so <c>/var</c> → <c>/private/var</c> cannot colour a comparison.</summary>
internal sealed partial class Scratch : IDisposable
{
    /// <summary>glibc's libc is <c>libc.so.6</c>; "libc" alone finds libSystem on macOS and musl.</summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void ResolveLibc() =>
        NativeLibrary.SetDllImportResolver(
            typeof(Scratch).Assembly,
            (name, _, _) =>
                OperatingSystem.IsLinux()
                && string.Equals(name, "libc", StringComparison.Ordinal)
                && NativeLibrary.TryLoad("libc.so.6", out var handle)
                    ? handle
                    : 0
        );

    public Scratch()
    {
        Root = FsPath.Canonicalize(Directory.CreateTempSubdirectory("fskit-").FullName);
    }

    public string Root { get; }

    public string this[string relative] =>
        Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public string Write(string relative, string body = "")
    {
        var path = this[relative];
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, body);
        return path;
    }

    public string[] Entries(string relative = "") =>
        [
            .. Directory
                .GetFileSystemEntries(this[relative])
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal)!,
        ];

    /// <summary>A symlink at <paramref name="link"/>; skips the test where this OS user cannot make one (Windows without Developer Mode).</summary>
    public string Symlink(string link, string target)
    {
        var path = this[link];
        try
        {
            File.CreateSymbolicLink(path, target);
        }
        catch (IOException) when (OperatingSystem.IsWindows())
        {
            Assert.Skip("symlinks need Developer Mode on Windows");
        }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
        {
            Assert.Skip("symlinks need Developer Mode on Windows");
        }
        return path;
    }

    public void HardLink(string existing, string link)
    {
        var ok = OperatingSystem.IsWindows()
            ? CreateHardLink(this[link], this[existing], 0)
            : Link(this[existing], this[link]) == 0;
        Assert.True(ok, $"hard link failed: {Marshal.GetLastPInvokeError()}");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [LibraryImport(
        "libc",
        EntryPoint = "link",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true
    )]
    private static partial int Link(string existing, string link);

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "CreateHardLinkW",
        StringMarshalling = StringMarshalling.Utf16,
        SetLastError = true
    )]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateHardLink(string link, string existing, nint security);
}
