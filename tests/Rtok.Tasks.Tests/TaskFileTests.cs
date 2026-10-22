using Rtok.Tasks.Disk;

namespace Rtok.Tasks.Tests;

public sealed class TaskFileTests
{
    [Theory]
    [InlineData("Ship the: allocator!", "ship-the-allocator")]
    [InlineData("Fix ../../etc/passwd", "fix-etc-passwd")]
    [InlineData("Привет", "task")]
    [InlineData("  --A--  ", "a")]
    public void Slugs_keep_ascii_letters_and_digits(string title, string slug) =>
        Assert.Equal(slug, TaskFile.Slug(title));

    [Fact]
    public void Slugs_stop_at_48_characters() =>
        Assert.Equal(
            new string('a', 47),
            TaskFile.Slug(new string('a', 47) + " " + new string('b', 10))
        );

    [Theory]
    [InlineData("R1 - ship.md", "R1")]
    [InlineData("r5 - b.md", "R5")]
    [InlineData("README.md", null)]
    [InlineData("R2 - notes.txt", null)]
    [InlineData("R2-notes.md", null)]
    public void Only_id_dash_slug_md_files_are_tasks(string name, string? id) =>
        Assert.Equal(id, TaskFile.FileId(name)?.ToString());

    [Fact]
    public void A_minimal_task_renders_only_the_required_keys()
    {
        var task = new PlanTask
        {
            Id = TaskId.Parse("A12"),
            Title = "Ship the thing",
            Description = "Why the task exists and what done means.\n",
            CreatedAt = 1790000000,
            UpdatedAt = 1790000000,
        };
        Assert.Equal(
            "---\nid: A12\ntitle: Ship the thing\nstatus: open\ncreated_at: 1790000000\nupdated_at: 1790000000\n---\n\nWhy the task exists and what done means.\n",
            TaskFile.Render(task)
        );
    }

    [Fact]
    public void Every_field_round_trips_including_crlf_and_a_body_with_fences()
    {
        var task = new PlanTask
        {
            Id = TaskId.Parse("R4"),
            Title = "Title: with \"quotes\" and # hash",
            Description = "Body\n---\nstill body",
            Status = TaskState.InProgress,
            Parent = TaskId.Parse("R2"),
            CreatedAt = 10,
            UpdatedAt = 20,
            Priority = 0,
            Assignee = "3f1c-uuid",
            BlockedBy = [TaskId.Parse("R1")],
            ConflictsWith = [TaskId.Parse("R3"), TaskId.Parse("R2")],
            Parallel = true,
        };
        task.Normalize();
        var text = TaskFile.Render(task);
        Assert.Contains(
            "blocked_by:\n  - R1\nconflicts_with:\n  - R2\n  - R3\nparallel: true\n---\n",
            text,
            StringComparison.Ordinal
        );
        foreach (
            var variant in (string[])[text, text.Replace("\n", "\r\n", StringComparison.Ordinal)]
        )
        {
            var back = TaskFile.Parse(variant);
            Assert.Equal(TaskFile.Render(back), text);
        }
    }

    [Fact]
    public void Reads_unindented_lists_and_files_without_claim_keys()
    {
        var task = TaskFile.Parse(
            "---\nid: r3\ntitle: Old\nstatus: done\ncreated_at: 1\nupdated_at: 2\nblocked_by:\n- R2\n- R1\n- R2\nassignee: ''\n---\n"
        );
        Assert.Equal("R1 R2", string.Join(' ', task.BlockedBy));
        Assert.Equal(PlanTask.DefaultPriority, task.Priority);
        Assert.Null(task.Assignee);
        Assert.Equal("", task.Description);
    }

    [Theory]
    [InlineData("id: R1\n", "no front matter (the file must start with ---)")]
    [InlineData("---\nid: R1\n", "front matter is not closed with ---")]
    [InlineData("---\nid: R1\nstatus: open\n---\n", "missing `title` in the front matter")]
    [InlineData(
        "---\nid: R1\ntitle: T\nstatus: open\nblocked_by:\n  - # later\n---\n",
        "an empty item in `blocked_by`"
    )]
    public void Broken_files_say_why(string text, string message) =>
        Assert.Equal(message, Assert.Throws<TaskException>(() => TaskFile.Parse(text)).Message);
}
