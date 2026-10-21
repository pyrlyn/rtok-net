namespace Rtok.Testing;

/// <summary>
/// A fresh directory under the system temp dir that deletes itself on dispose, so a test leaves
/// nothing behind (rtok T644). Prefer <see cref="MemFs"/> when the code under test takes an
/// <c>IFileSystem</c>; use a TempDir only for real-disk behaviour.
/// </summary>
public sealed class TempDir : IDisposable
{
    public TempDir(string prefix = "rtok-test-")
    {
        Path = Directory.CreateTempSubdirectory(prefix).FullName;
    }

    /// <summary>The directory's full path.</summary>
    public string Path { get; }

    /// <summary>A path inside the directory; nothing is created.</summary>
    public string Combine(params ReadOnlySpan<string> parts) =>
        System.IO.Path.Join(Path, System.IO.Path.Join(parts));

    /// <summary>Writes <paramref name="content"/> to a file inside the directory, creating its
    /// parents, and returns the file's full path.</summary>
    public string Write(string relative, string content)
    {
        var file = Combine(relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
        return file;
    }

    public void Dispose()
    {
        if (!Directory.Exists(Path))
        {
            return;
        }

        // Read-only entries (git objects, a test's own chmod) block deletion on Windows.
        foreach (
            var entry in Directory.EnumerateFileSystemEntries(
                Path,
                "*",
                SearchOption.AllDirectories
            )
        )
        {
            File.SetAttributes(entry, FileAttributes.Normal);
        }

        Directory.Delete(Path, recursive: true);
    }
}
