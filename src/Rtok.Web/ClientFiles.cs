using System.Reflection;

namespace Rtok.Web;

/// <summary>The dashboard client's published files: embedded in this assembly, or a built
/// directory named by <c>RTOK_WEB_DIST</c>. Paths are relative and <c>/</c>-separated, as
/// <see cref="AssetPath.Decode"/> returns them.</summary>
public abstract class ClientFiles
{
    /// <summary>The variable that points the server at a built client directory.</summary>
    public const string DistVariable = "RTOK_WEB_DIST";

    /// <summary>Every file, for listing and tests.</summary>
    /// <returns>The relative paths.</returns>
    public abstract IEnumerable<string> List();

    /// <summary>Whether <paramref name="path"/> is a file.</summary>
    /// <param name="path">A decoded relative path.</param>
    /// <returns>True when it exists.</returns>
    public abstract bool Exists(string path);

    /// <summary>Opens a file that <see cref="Exists"/>.</summary>
    /// <param name="path">A decoded relative path.</param>
    /// <returns>The file's bytes.</returns>
    public abstract Stream Open(string path);

    /// <summary>The client this binary embeds.</summary>
    /// <returns>The files.</returns>
    public static ClientFiles Embedded() => new EmbeddedFiles(typeof(ClientFiles).Assembly);

    /// <summary>The client to serve: <paramref name="dist"/> when it is a built client directory,
    /// else the embedded one with a notice (a typo in a developer override must not take the
    /// dashboard down).</summary>
    /// <param name="dist">The value of <see cref="DistVariable"/>, or null.</param>
    /// <returns>The files and a line to print, or null.</returns>
    public static (ClientFiles Files, string? Notice) Resolve(string? dist)
    {
        if (string.IsNullOrEmpty(dist))
        {
            return (Embedded(), null);
        }
        if (File.Exists(Path.Join(dist, "index.html")))
        {
            return (new DirectoryFiles(Path.GetFullPath(dist)), null);
        }
        return (
            Embedded(),
            $"rtok web: {DistVariable}={dist} has no index.html; serving the embedded client"
        );
    }

    private sealed class EmbeddedFiles : ClientFiles
    {
        private const string Prefix = "client/";
        private readonly Assembly _assembly;
        private readonly HashSet<string> _names;

        public EmbeddedFiles(Assembly assembly)
        {
            _assembly = assembly;
            _names = assembly
                .GetManifestResourceNames()
                .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal))
                .Select(n => n[Prefix.Length..])
                .ToHashSet(StringComparer.Ordinal);
        }

        public override IEnumerable<string> List() => _names.Order(StringComparer.Ordinal);

        public override bool Exists(string path) => _names.Contains(path);

        public override Stream Open(string path) =>
            _assembly.GetManifestResourceStream(Prefix + path)
            ?? throw new FileNotFoundException("not an embedded client file", path);
    }

    private sealed class DirectoryFiles(string root) : ClientFiles
    {
        public override IEnumerable<string> List() =>
            Directory
                .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
                .Order(StringComparer.Ordinal);

        public override bool Exists(string path) => Full(path) is { } full && File.Exists(full);

        public override Stream Open(string path) =>
            File.OpenRead(
                Full(path) ?? throw new FileNotFoundException("outside the client", path)
            );

        // AssetPath already refuses separators and dot segments; this is the second lock.
        private string? Full(string path)
        {
            var full = Path.GetFullPath(Path.Join(root, path));
            return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                ? full
                : null;
        }
    }
}
