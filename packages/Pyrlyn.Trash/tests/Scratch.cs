namespace Pyrlyn.Trash.Tests;

/// <summary>A fresh directory under the temp dir, removed with whatever is left in it.</summary>
internal sealed class Scratch : IDisposable
{
    public Scratch()
    {
        Root = Directory.CreateTempSubdirectory("pyrlyn-trash-").FullName;
    }

    public string Root { get; }

    public string File(string relative, string text = "x")
    {
        var path = Path.Join(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, text);
        return path;
    }

    public string Dir(string relative) =>
        Directory.CreateDirectory(Path.Join(Root, relative)).FullName;

    public void Dispose()
    {
        foreach (var dir in Directory.EnumerateDirectories(Root, "*", SearchOption.AllDirectories))
        {
            if (!OperatingSystem.IsWindows())
            {
                System.IO.File.SetUnixFileMode(
                    dir,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                );
            }
        }
        Directory.Delete(Root, recursive: true);
    }
}
