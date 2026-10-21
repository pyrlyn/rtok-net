namespace Pyrlyn.Ignore;

/// <summary>The git facts a walk needs about a directory that holds <c>.git</c>.</summary>
internal sealed class GitRepo
{
    private GitRepo(string root, string? commonDir, bool isLinkedWorktree)
    {
        Root = root;
        CommonDir = commonDir;
        IsLinkedWorktree = isLinkedWorktree;
    }

    /// <summary>The checkout: the directory holding <c>.git</c>.</summary>
    public string Root { get; }

    /// <summary>The shared git directory (<c>info/exclude</c>, <c>config</c>), or <see langword="null"/> when a <c>.git</c> file points nowhere readable.</summary>
    public string? CommonDir { get; }

    /// <summary>A <c>.git</c> file whose git dir has a <c>commondir</c>: another checkout of a main repository. Submodules have none.</summary>
    public bool IsLinkedWorktree { get; }

    /// <summary>True when <paramref name="dir"/> holds a <c>.git</c> directory or file. One stat.</summary>
    public static bool Has(string dir) => Path.Exists(Path.Combine(dir, ".git"));

    /// <summary>The repository rooted at <paramref name="dir"/>, or <see langword="null"/> when it has no <c>.git</c>.</summary>
    public static GitRepo? At(string dir)
    {
        var dotGit = Path.Combine(dir, ".git");
        if (Directory.Exists(dotGit))
        {
            return new GitRepo(dir, dotGit, isLinkedWorktree: false);
        }
        if (!File.Exists(dotGit))
        {
            return null;
        }
        var gitDir = ReadGitDir(dotGit, dir);
        if (gitDir is null || !Directory.Exists(gitDir))
        {
            return new GitRepo(dir, null, isLinkedWorktree: false);
        }
        var common = ReadLine(Path.Combine(gitDir, "commondir"));
        return common is null
            ? new GitRepo(dir, gitDir, isLinkedWorktree: false)
            : new GitRepo(dir, Path.GetFullPath(common, gitDir), isLinkedWorktree: true);
    }

    /// <summary>The nearest repository at or above <paramref name="dir"/>.</summary>
    public static GitRepo? Find(string dir)
    {
        for (var d = dir; d is not null; d = Path.GetDirectoryName(d))
        {
            if (At(d) is { } repo)
            {
                return repo;
            }
        }
        return null;
    }

    /// <summary>
    /// The global excludes file: <c>core.excludesFile</c> from the global or repository config
    /// (the last one set wins), else <c>$XDG_CONFIG_HOME/git/ignore</c>.
    /// </summary>
    public string? GlobalExcludesFile(string? home, string? xdgConfigHome)
    {
        var xdg =
            !string.IsNullOrEmpty(xdgConfigHome) ? xdgConfigHome
            : home is null ? null
            : Path.Combine(home, ".config");
        string?[] configs =
        [
            xdg is null ? null : Path.Combine(xdg, "git", "config"),
            home is null ? null : Path.Combine(home, ".gitconfig"),
            CommonDir is null ? null : Path.Combine(CommonDir, "config"),
        ];
        string? set = null;
        foreach (var config in configs.OfType<string>())
        {
            set = ExcludesFileIn(config) ?? set;
        }
        if (set is not null)
        {
            return set.StartsWith("~/", StringComparison.Ordinal) && home is not null
                ? Path.Combine(home, set[2..])
                : set;
        }
        return xdg is null ? null : Path.Combine(xdg, "git", "ignore");
    }

    /// <summary><c>core.excludesFile</c> in one git config file; sections and keys are case-insensitive.</summary>
    private static string? ExcludesFileIn(string config)
    {
        string[] lines;
        try
        {
            if (!File.Exists(config))
            {
                return null;
            }
            lines = File.ReadAllLines(config);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        string? found = null;
        var inCore = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                var end = line.IndexOf(']', StringComparison.Ordinal);
                inCore =
                    end > 0
                    && line[1..end].Trim().Equals("core", StringComparison.OrdinalIgnoreCase);
                line = end > 0 ? line[(end + 1)..].Trim() : "";
            }
            if (!inCore || line.Length == 0 || line[0] is '#' or ';')
            {
                continue;
            }
            var eq = line.IndexOf('=', StringComparison.Ordinal);
            if (
                eq > 0
                && line[..eq].Trim().Equals("excludesfile", StringComparison.OrdinalIgnoreCase)
            )
            {
                var value = line[(eq + 1)..].Trim();
                found =
                    value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
            }
        }
        return found;
    }

    private static string? ReadGitDir(string dotGitFile, string dir)
    {
        var line = ReadLine(dotGitFile);
        const string Prefix = "gitdir:";
        return line is not null && line.StartsWith(Prefix, StringComparison.Ordinal)
            ? Path.GetFullPath(line[Prefix.Length..].Trim(), dir)
            : null;
    }

    private static string? ReadLine(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }
            using var reader = new StreamReader(path);
            return reader.ReadLine()?.Trim();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
