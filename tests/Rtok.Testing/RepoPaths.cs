namespace Rtok.Testing;

/// <summary>Paths inside the repository the tests run from (a worktree counts).</summary>
public static class RepoPaths
{
    /// <summary>The directory holding <c>rtok.slnx</c>, found upwards from the test binary, or
    /// from the working directory for a binary built outside the repo (a file-based app).</summary>
    public static string Root { get; } =
        FindRoot(AppContext.BaseDirectory)
        ?? FindRoot(Environment.CurrentDirectory)
        ?? throw new InvalidOperationException(
            $"no rtok.slnx above {AppContext.BaseDirectory} or {Environment.CurrentDirectory}"
        );

    /// <summary>A file under <c>tests/fixtures</c>.</summary>
    public static string Fixture(params ReadOnlySpan<string> parts) =>
        Path.Join(Root, "tests", "fixtures", Path.Join(parts));

    private static string? FindRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Join(dir.FullName, "rtok.slnx")))
            {
                return dir.FullName;
            }
        }

        return null;
    }
}
