using System.Security.Cryptography;
using System.Text;
using Pyrlyn.FsKit;
using Rtok.Store;

namespace Rtok.Plugins.Docs;

/// <summary>
/// A workspace's own docs: <c>llms-full.txt</c> or <c>llms.txt</c> at its root, plus every local
/// file or directory that text links to. They are stored as package <see cref="Name"/> under
/// <see cref="Version"/>, the canonical workspace path, so workspaces sharing one store never
/// see each other's text (rtok's rule); callers say <c>local</c>.
/// </summary>
public static class LocalDocs
{
    /// <summary>The package name local docs are stored and asked for under.</summary>
    public const string Name = "llms";

    /// <summary>The most linked files read; a linked directory counts each file in it.</summary>
    public const int MaxFiles = 200;

    /// <summary>A file larger than this is skipped.</summary>
    public const long MaxFileBytes = 1 << 20;

    private static readonly string[] Roots = ["llms-full.txt", "llms.txt"];
    private static readonly string[] Extensions = [".md", ".markdown", ".mdx", ".txt"];

    /// <summary>The version local docs of <paramref name="cwd"/> are stored under: <c>local:</c> and its canonical path.</summary>
    public static string Version(string cwd) => "local:" + FsPath.Canon(cwd);

    /// <summary>
    /// The items of <paramref name="cwd"/>'s local docs and the SHA-256 of every source read, or
    /// null when it has no <c>llms.txt</c> or nothing in it and its links has text. An
    /// <c>llms.txt</c> section is titled by its heading; a linked file's by its path relative to
    /// the workspace, then <c>#</c> and the heading. A link that leaves the workspace, symlinks
    /// followed, is ignored.
    /// </summary>
    public static (string Sha256, IReadOnlyList<DocEntry> Items)? Read(string cwd)
    {
        ArgumentNullException.ThrowIfNull(cwd);
        var root = FsPath.Resolved(cwd);
        var llms = Roots.Select(n => Path.Join(root, n)).FirstOrDefault(File.Exists);
        if (llms is null || ReadText(llms) is not { } body)
        {
            return null;
        }
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Hash(sha, Path.GetFileName(llms), body);
        var items = new List<DocEntry>(LlmsText.Split(body));
        var seen = new HashSet<string>(StringComparer.Ordinal) { FsPath.Resolved(llms) };
        foreach (var file in Linked(root, body).Where(seen.Add).Take(MaxFiles))
        {
            if (ReadText(file) is not { } text)
            {
                continue;
            }
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            Hash(sha, rel, text);
            items.AddRange(
                LlmsText
                    .Split(text, "local", rel)
                    .Select(e =>
                        ReferenceEquals(e.Path, rel) ? e : e with { Path = rel + "#" + e.Path }
                    )
            );
        }
        return items.Count == 0 ? null : (Convert.ToHexStringLower(sha.GetHashAndReset()), items);
    }

    private static IEnumerable<string> Linked(string root, string body)
    {
        foreach (var link in LlmsText.LocalLinks(body))
        {
            string target;
            try
            {
                target = FsPath.Contain(root, Uri.UnescapeDataString(link));
            }
            catch (Exception e)
                when (e is UnauthorizedAccessException or IOException or ArgumentException)
            {
                continue;
            }
            if (File.Exists(target))
            {
                yield return target;
            }
            else if (Directory.Exists(target))
            {
                foreach (var file in Files(target))
                {
                    yield return file;
                }
            }
        }
    }

    private static List<string> Files(string dir)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = 4,
            IgnoreInaccessible = true,
            AttributesToSkip =
                FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        };
        try
        {
            var files = Directory
                .EnumerateFiles(dir, "*", options)
                .Where(f =>
                    Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)
                )
                .Take(MaxFiles)
                .ToList();
            files.Sort(StringComparer.Ordinal);
            return files;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string? ReadText(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length <= MaxFileBytes ? File.ReadAllText(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void Hash(IncrementalHash sha, string name, string text)
    {
        sha.AppendData(Encoding.UTF8.GetBytes(name));
        sha.AppendData([0]);
        sha.AppendData(Encoding.UTF8.GetBytes(text));
        sha.AppendData([0]);
    }
}
