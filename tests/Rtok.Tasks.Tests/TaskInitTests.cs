using Rtok.Testing;

namespace Rtok.Tasks.Tests;

public sealed class TaskInitTests : IDisposable
{
    private readonly TempDir _temp = new();

    public TaskInitTests() => GitRepo.Init(_temp.Path, "https://github.com/me/app.git");

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Init_adds_the_tasks_table_and_keeps_the_rest()
    {
        _temp.Write(".rtok.toml", "# mine\n[proxy]\nport = 9999\n");
        Directory.CreateDirectory(_temp.Combine("src"));

        var file = TaskInit.Write(_temp.Combine("src"), null, "at");

        Assert.Equal(_temp.Combine(".rtok.toml"), file);
        var text = File.ReadAllText(file);
        Assert.StartsWith("# mine\n[proxy]\nport = 9999\n", text, StringComparison.Ordinal);
        Assert.Contains(
            "[tasks]\nadapter = \"disk\"\nprefix = \"AT\"\n",
            text,
            StringComparison.Ordinal
        );

        TaskInit.Write(_temp.Path, "github", null);
        Assert.Contains(
            "[tasks]\nadapter = \"github\"\nprefix = \"AT\"\n",
            File.ReadAllText(file),
            StringComparison.Ordinal
        );
        TaskInit.Write(_temp.Path, null, null);
        Assert.Contains("adapter = \"github\"", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void Init_refuses_bad_flags_and_writes_nothing()
    {
        Assert.Equal(
            "rtok task init: --adapter must be one of disk, github, gitlab",
            Assert.Throws<TaskException>(() => TaskInit.Write(_temp.Path, "jira", null)).Message
        );
        Assert.Throws<TaskException>(() => TaskInit.Write(_temp.Path, null, "toolongprefix"));
        _temp.Write(".rtok.toml", "tasks = 1\n");
        Assert.Contains(
            "tasks is not a table",
            Assert.Throws<TaskException>(() => TaskInit.Write(_temp.Path, null, null)).Message,
            StringComparison.Ordinal
        );
        Assert.Equal("tasks = 1\n", File.ReadAllText(_temp.Combine(".rtok.toml")));
    }

    [Fact]
    public void Init_needs_a_checkout()
    {
        using var bare = new TempDir();
        Assert.Equal(
            "rtok task init: not inside a git checkout",
            Assert.Throws<TaskException>(() => TaskInit.Write(bare.Path, null, null)).Message
        );
    }
}
