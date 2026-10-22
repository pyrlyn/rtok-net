namespace Rtok.Plugins.Docs;

/// <summary>
/// Finds a dependency's pinned version in the lockfiles of a workspace. <c>Cargo.lock</c> is read
/// at the workspace root, as rtok did; .NET keeps one <c>packages.lock.json</c> per project, so
/// those are read at the root and up to <see cref="MaxDepth"/> directories down, in ordinal
/// order. The first lockfile that pins the name wins. A missing, unreadable or malformed
/// lockfile pins nothing.
/// </summary>
public static class Lockfiles
{
    /// <summary>How many directories below the workspace root a <c>packages.lock.json</c> is looked for.</summary>
    public const int MaxDepth = 3;

    /// <summary>A lockfile larger than this is skipped.</summary>
    public const long MaxBytes = 16 << 20;

    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        "node_modules",
        "target",
        "artifacts",
    };

    /// <summary>The pinned version of <paramref name="name"/> under <paramref name="root"/>, or null.</summary>
    public static PinnedPackage? Resolve(string root, string name)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(name);
        if (
            Read(Path.Join(root, "Cargo.lock")) is { } cargo
            && CargoLock.Resolve(cargo, name) is { } crate
        )
        {
            return crate;
        }
        foreach (var file in NuGetLockFiles(root))
        {
            if (Read(file) is { } text && NuGetLock.Resolve(text, name) is { } package)
            {
                return package;
            }
        }
        return null;
    }

    private static IEnumerable<string> NuGetLockFiles(string root)
    {
        var level = new List<string> { root };
        for (var depth = 0; depth <= MaxDepth && level.Count > 0; depth++)
        {
            var next = new List<string>();
            foreach (var dir in level)
            {
                var file = Path.Join(dir, "packages.lock.json");
                if (File.Exists(file))
                {
                    yield return file;
                }
                if (depth < MaxDepth)
                {
                    next.AddRange(Children(dir));
                }
            }
            level = next;
        }
    }

    private static List<string> Children(string dir)
    {
        try
        {
            var children = new DirectoryInfo(dir)
                .EnumerateDirectories()
                .Where(d =>
                    !d.Name.StartsWith('.')
                    && !Skipped.Contains(d.Name)
                    && !d.Attributes.HasFlag(FileAttributes.ReparsePoint)
                )
                .Select(d => d.FullName)
                .ToList();
            children.Sort(StringComparer.Ordinal);
            return children;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string? Read(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length <= MaxBytes ? File.ReadAllText(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether a lockfile's name or version is safe to use as a path segment and in a URL:
    /// letters, digits and <c>. _ - +</c>, no leading dot, at most 128 characters.
    /// </summary>
    internal static bool Safe(string value) =>
        value.Length is > 0 and <= 128
        && value[0] != '.'
        && !value.Contains("..", StringComparison.Ordinal)
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '+');
}
