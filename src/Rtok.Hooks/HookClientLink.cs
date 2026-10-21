using Pyrlyn.FsKit;

namespace Rtok.Hooks;

/// <summary>
/// Puts <c>rtok-hook</c> on PATH: a symlink beside the first <c>rtok</c> on PATH, made only when
/// that <c>rtok</c> is the running binary, to the client shipped beside it. It never replaces a
/// regular file or a link that is not an <c>rtok-hook</c>. Runs at the start of
/// <c>rtok hook --serve</c>; every failure is soft.
/// </summary>
public static class HookClientLink
{
    private const string Client = "rtok-hook";

    /// <param name="path">The PATH value.</param>
    /// <param name="runningExe">The running <c>rtok</c>'s path.</param>
    public static HookLinkResult Ensure(string? path, string runningExe)
    {
        ArgumentNullException.ThrowIfNull(runningExe);
        if (OperatingSystem.IsWindows())
        {
            return HookLinkResult.Unsupported;
        }
        try
        {
            var running = FsPath.Canonicalize(runningExe);
            var client = Path.Join(Path.GetDirectoryName(running), Client);
            if (!File.Exists(client))
            {
                return HookLinkResult.NoClient;
            }
            var dir = (path ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(d =>
                    Path.IsPathFullyQualified(d) && File.Exists(Path.Join(d, "rtok"))
                );
            if (dir is null)
            {
                return HookLinkResult.NotOnPath;
            }
            if (!FsPath.SameFile(Path.Join(dir, "rtok"), running))
            {
                return HookLinkResult.NotRunningExe;
            }
            return Place(Path.Join(dir, Client), client);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return HookLinkResult.Failed;
        }
    }

    private static HookLinkResult Place(string target, string client)
    {
        var info = new FileInfo(target);
        var result = HookLinkResult.Linked;
        if (info.LinkTarget is { } linked)
        {
            if (info.Exists && FsPath.SameFile(target, client))
            {
                return HookLinkResult.AlreadyLinked;
            }
            if (!string.Equals(Path.GetFileName(linked), Client, StringComparison.Ordinal))
            {
                return HookLinkResult.Foreign;
            }
            result = HookLinkResult.Relinked;
        }
        else if (info.Exists || Directory.Exists(target))
        {
            return HookLinkResult.Foreign;
        }
        // A temporary link renamed over the name: the swap is atomic.
        var tmp = Path.Join(
            Path.GetDirectoryName(target),
            $".{Client}.{Environment.ProcessId}.tmp"
        );
        File.Delete(tmp);
        File.CreateSymbolicLink(tmp, client);
        File.Move(tmp, target, overwrite: true);
        return result;
    }
}
