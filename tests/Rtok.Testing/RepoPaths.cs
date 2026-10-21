namespace Rtok.Testing;

/// <summary>Paths inside the repository the tests run from (a worktree counts).</summary>
public static class RepoPaths
{
    /// <summary>The directory holding <c>rtok.slnx</c>, found upwards from the test binary.</summary>
    public static string Root { get; } = FindRoot(AppContext.BaseDirectory);

    /// <summary>A file under <c>tests/fixtures</c>.</summary>
    public static string Fixture(params ReadOnlySpan<string> parts) =>
        Path.Join(Root, "tests", "fixtures", Path.Join(parts));

    private static string FindRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Join(dir.FullName, "rtok.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException($"no rtok.slnx above {start}");
    }
}
