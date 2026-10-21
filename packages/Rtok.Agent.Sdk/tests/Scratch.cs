using Pyrlyn.FsKit;

namespace Rtok.Agent.Sdk.Tests;

/// <summary>A fresh directory under the temp dir, canonical so <c>/var</c> → <c>/private/var</c> cannot colour a comparison.</summary>
internal sealed class Scratch : IDisposable
{
    public string Root { get; } =
        FsPath.Canonicalize(Directory.CreateTempSubdirectory("agent-sdk-").FullName);

    public string this[string relative] =>
        Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public string Write(string relative, string body = "")
    {
        var path = this[relative];
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, body);
        return path;
    }

    public string Read(string relative) => File.ReadAllText(this[relative]);

    /// <summary>File names in <paramref name="relative"/>, sorted; empty when it does not exist.</summary>
    public string[] Entries(string relative) =>
        Directory.Exists(this[relative])
            ?
            [
                .. Directory
                    .GetFileSystemEntries(this[relative])
                    .Select(p => Path.GetFileName(p))
                    .Order(StringComparer.Ordinal),
            ]
            : [];

    /// <summary>The bare name <c>rtok</c>, <c>rtok.exe</c> or any path ending in one counts as rtok's binary.</summary>
    public static bool IsRtok(string s) =>
        s.Replace('\\', '/').Split('/')[^1].ToLowerInvariant() is "rtok" or "rtok.exe";

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
