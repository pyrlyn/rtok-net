using System.Diagnostics;
using Pyrlyn.FsKit;

namespace Pyrlyn.Ignore.Tests;

/// <summary>A fresh directory under the temp dir, with a fake home beside the tree so no real git config leaks in.</summary>
internal sealed class Scratch : IDisposable
{
    public Scratch()
    {
        var top = FsPath.Canonicalize(Directory.CreateTempSubdirectory("ignore-").FullName);
        Top = top;
        Root = Path.Combine(top, "repo");
        Home = Path.Combine(top, "home");
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Home);
    }

    public string Top { get; }

    public string Root { get; }

    public string Home { get; }

    public string this[string relative] =>
        Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Options that read git's global config from the fake home only.</summary>
    public WalkOptions Options => new() { Home = Home, XdgConfigHome = null };

    public string Write(string relative, string body = "")
    {
        var path = this[relative];
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, body);
        return path;
    }

    public void Files(params string[] relatives)
    {
        foreach (var r in relatives)
        {
            if (r.EndsWith('/'))
            {
                Directory.CreateDirectory(this[r]);
            }
            else
            {
                Write(r, r);
            }
        }
    }

    /// <summary>What the walker yields under <paramref name="relative"/>: files and symlinks, sorted.</summary>
    public string[] Walked(string relative = "", WalkOptions? options = null) =>
        [
            .. new RepoWalker(this[relative], options ?? Options)
                .Walk()
                .Where(e => !e.IsDirectory)
                .Select(e => e.RelativePath)
                .Order(StringComparer.Ordinal),
        ];

    /// <summary>Run git in <paramref name="dir"/> with the fake home and no system config; its stdout.</summary>
    public string Git(string dir, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args)
        {
            start.ArgumentList.Add(a);
        }
        start.Environment["HOME"] = Home;
        start.Environment["USERPROFILE"] = Home;
        start.Environment.Remove("XDG_CONFIG_HOME");
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        start.Environment.Remove("GIT_DIR");
        start.Environment.Remove("GIT_WORK_TREE");
        start.Environment.Remove("GIT_INDEX_FILE");
        using var git = Process.Start(start)!;
        var stdout = git.StandardOutput.ReadToEndAsync();
        var stderr = git.StandardError.ReadToEnd();
        git.WaitForExit();
        Assert.True(git.ExitCode == 0, $"git {string.Join(' ', args)}: {stderr}");
        return stdout.Result;
    }

    /// <summary><c>git init</c> at <paramref name="relative"/>, case-sensitive matching whatever the filesystem.</summary>
    public void Init(string relative = "")
    {
        var dir = this[relative];
        Directory.CreateDirectory(dir);
        Git(dir, "init", "-q");
        Git(dir, "config", "core.ignorecase", "false");
    }

    /// <summary>What git lists under <paramref name="relative"/>: tracked plus untracked, not ignored.</summary>
    public string[] Listed(string relative = "") =>
        [
            .. Git(this[relative], "ls-files", "--others", "--cached", "--exclude-standard", "-z")
                .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Order(StringComparer.Ordinal),
        ];

    public void Dispose()
    {
        try
        {
            Directory.Delete(Top, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
