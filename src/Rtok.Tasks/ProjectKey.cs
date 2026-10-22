using LibGit2Sharp;
using Pyrlyn.FsKit;
using Rtok.Config;
using Rtok.Git;

namespace Rtok.Tasks;

/// <summary>
/// What names a project across its checkouts: the origin remote, else the main checkout.
/// Linked worktrees and fresh clones of one repository share the key. The origin is read
/// through <see cref="GitRepository"/> (N21); git is never run.
/// </summary>
public static class ProjectKey
{
    /// <summary>
    /// The key: <c>github.com/owner/repo</c> for a network origin (lower-case, no scheme, user
    /// or <c>.git</c>), the remote's absolute path for a local one, else the main checkout.
    /// </summary>
    /// <param name="root">The project.</param>
    /// <returns>The key.</returns>
    public static string Of(ProjectRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return Origin(root) is { } url ? FromUrl(url, root.Main) : Local(root.Main);
    }

    /// <summary>The project's short name: the origin's last path segment without <c>.git</c>, else the main checkout's directory name.</summary>
    /// <param name="root">The project.</param>
    /// <returns>The name, possibly empty.</returns>
    public static string Name(ProjectRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (Origin(root) is { } url)
        {
            var trimmed = StripGit(url.TrimEnd('/', '\\'));
            var cut = trimmed.LastIndexOfAny(['/', ':', '\\']);
            var last = trimmed[(cut + 1)..];
            if (last.Length > 0)
            {
                return last;
            }
        }
        return Path.GetFileName(root.Main.TrimEnd('/', '\\'));
    }

    /// <summary>The key of an origin URL.</summary>
    /// <param name="url">The remote URL as configured.</param>
    /// <param name="main">The main checkout, which relative local remotes resolve from.</param>
    /// <returns>The key.</returns>
    public static string FromUrl(string url, string main)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(main);
        var u = url.Trim();
        if (u.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var rest = u["file://".Length..];
            var slash = rest.IndexOf('/', StringComparison.Ordinal);
            // file://host/path: only an empty host or localhost is this machine.
            rest = slash < 0 ? rest : rest[slash..];
            // file:///C:/x is C:/x.
            if (rest.Length > 2 && rest[0] == '/' && char.IsAsciiLetter(rest[1]) && rest[2] == ':')
            {
                rest = rest[1..];
            }
            return Local(FsPath.Normalize(main, rest));
        }
        if (IsNetwork(u))
        {
            return Network(u);
        }
        return Local(FsPath.Normalize(main, u));
    }

    private static bool IsNetwork(string url)
    {
        if (url.Contains("://", StringComparison.Ordinal))
        {
            return true;
        }
        // scp-like host:path, where a one-letter host is a Windows drive.
        var colon = url.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 1)
        {
            return false;
        }
        var slash = url.IndexOfAny(['/', '\\']);
        return slash < 0 || slash > colon;
    }

    private static string Network(string url)
    {
        var u = StripGit(url.TrimEnd('/'));
        var scheme = u.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            u = u[(scheme + 3)..];
        }
        var firstSlash = u.IndexOf('/', StringComparison.Ordinal);
        var at = u.IndexOf('@', StringComparison.Ordinal);
        if (at >= 0 && (firstSlash < 0 || at < firstSlash))
        {
            u = u[(at + 1)..];
        }
        if (scheme < 0)
        {
            // scp-like: host:path.
            var colon = u.IndexOf(':', StringComparison.Ordinal);
            u = $"{u[..colon]}/{u[(colon + 1)..].TrimStart('/')}";
        }
        return u.ToLowerInvariant();
    }

    private static string Local(string path)
    {
        var real = FsPath.TryCanonicalize(path, out var canonical) ? canonical : path;
        var trimmed = real.Length > 1 ? real.TrimEnd('/', '\\') : real;
        return StripGit(trimmed).Replace('\\', '/');
    }

    private static string StripGit(string s) =>
        s.EndsWith(".git", StringComparison.Ordinal) ? s[..^4] : s;

    /// <summary>The <c>origin</c> remote's URL, as git resolves the repository's config; null
    /// when there is none or the repository cannot be opened.</summary>
    internal static string? Origin(ProjectRoot root)
    {
        try
        {
            using var repo = GitRepository.Open(root.Checkout);
            var url = repo?.Network.Remotes["origin"]?.Url;
            return string.IsNullOrWhiteSpace(url) ? null : url;
        }
        catch (LibGit2SharpException)
        {
            return null;
        }
    }
}
