namespace Rtok.Tasks.Tests;

public sealed class TaskTextTests
{
    private static PlanTask Task(string id, string title, TaskState status = TaskState.Open) =>
        new()
        {
            Id = TaskId.Parse(id),
            Title = title,
            Status = status,
            Parent = TaskId.Parse(id).Parent,
        };

    [Fact]
    public void The_table_indents_subtasks_and_pads_the_columns()
    {
        Assert.Equal(
            "A7      open         Pulled\n  A7.1  open         Leaf\nA8      open         First\n",
            TaskText.Table([Task("A7", "Pulled"), Task("A7.1", "Leaf"), Task("A8", "First")])
        );
        Assert.Equal("", TaskText.Table([]));
        var marked = Task("K1", "First", TaskState.InProgress);
        marked.Parallel = true;
        marked.ConflictsWith = [TaskId.Parse("K2"), TaskId.Parse("K3")];
        Assert.Equal(
            "K1  in-progress  First  [parallel]  [conflicts: K2, K3]\n",
            TaskText.Table([marked])
        );
    }

    [Fact]
    public void Details_list_only_the_set_fields()
    {
        var t = Task("TD1", "Parent");
        t.Description = "Why it exists.";
        Assert.Equal(
            "TD1  Parent\nstatus: open\nsubtasks: TD1.1\n\nWhy it exists.\n",
            TaskText.Details(new TaskView(t, [TaskId.Parse("TD1.1")]))
        );
        var full = Task("A2.1", "Leaf", TaskState.InProgress);
        full.Priority = 0;
        full.Assignee = "agent-1";
        full.BlockedBy = [TaskId.Parse("A1")];
        full.Parallel = true;
        full.External = new ExternalRef("github", 9, "https://x/9", null);
        Assert.Equal(
            "A2.1  Leaf\nstatus: in-progress\nparent: A2\npriority: 0\nassignee: agent-1\nblocked by: A1\nparallel: yes\nlink: https://x/9\n",
            TaskText.Details(new TaskView(full, []))
        );
    }

    [Fact]
    public void Ready_marks_stale_parallel_and_conflicting_tasks()
    {
        var k3 = Task("K3", "Third");
        k3.Parallel = true;
        Assert.Equal(
            "K2  Second  (stale)  (conflicts with K1 in progress)\nK3  Third  [parallel]\n",
            TaskText.Ready([
                new ReadyItem(Task("K2", "Second"), true, [TaskId.Parse("K1")]),
                new ReadyItem(k3, false, []),
            ])
        );
        Assert.Equal("no ready task\n", TaskText.Ready([]));
    }
}
