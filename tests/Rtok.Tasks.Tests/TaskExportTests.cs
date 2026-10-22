using Rtok.Config;
using Rtok.Tasks.Export;

namespace Rtok.Tasks.Tests;

public sealed class TaskExportTests
{
    private static PlanTask Task(string id, TaskState status = TaskState.Open) =>
        new()
        {
            Id = TaskId.Parse(id),
            Title = "Title",
            Description = "Body",
            Status = status,
            Parent = TaskId.Parse(id).Parent,
        };

    [Fact]
    public void A_missing_issue_is_created_and_a_finished_one_is_skipped()
    {
        var (actions, unchanged, skipped) = TaskExport.Diff(
            [Task("R2.1"), Task("R1"), Task("R3", TaskState.Done)],
            [],
            claims: true
        );
        Assert.Equal(["R1", "R2.1"], actions.Select(a => a.Id.ToString()), StringComparer.Ordinal);
        Assert.All(actions, a => Assert.Equal(ExportKind.Create, a.Kind));
        Assert.Equal((0, 1), (unchanged, skipped));
    }

    [Fact]
    public void A_finished_parent_of_an_active_subtask_is_created_finished()
    {
        var (actions, _, skipped) = TaskExport.Diff(
            [Task("R1", TaskState.Done), Task("R1.1")],
            [],
            claims: true
        );
        Assert.Equal(
            ["create R1 status", "create R1.1 "],
            actions.Select(a => $"{a.Kind.Word()} {a.Id} {string.Join(',', a.Fields)}"),
            StringComparer.Ordinal
        );
        Assert.Equal(0, skipped);
    }

    [Fact]
    public void Drift_names_the_fields_and_picks_the_kind()
    {
        var open = Task("R1");
        open.Description = "New\r\n";
        var stale = Task("R1");
        var update = TaskExport.Diff([open], [stale], true).Actions.Single();
        Assert.Equal((ExportKind.Update, "body"), (update.Kind, string.Join(',', update.Fields)));

        var done = Task("R1", TaskState.Done);
        done.Title = "Renamed";
        var close = TaskExport.Diff([done], [stale], true).Actions.Single();
        Assert.Equal(
            (ExportKind.Close, "title,status"),
            (close.Kind, string.Join(',', close.Fields))
        );
        Assert.Equal(
            ExportKind.Reopen,
            TaskExport.Diff([open], [done], true).Actions.Single().Kind
        );
    }

    [Fact]
    public void A_matching_issue_is_unchanged_and_claims_are_ignored_when_not_kept()
    {
        var local = Task("R1");
        local.Assignee = "agent";
        local.Description = " Body\n";
        var remote = Task("R1");
        var (actions, unchanged, _) = TaskExport.Diff([local], [remote], claims: false);
        Assert.Empty(actions);
        Assert.Equal(1, unchanged);
        Assert.Equal(["assignee"], TaskExport.Diff([local], [remote], true).Actions[0].Fields);
    }

    [Fact]
    public void Markers_count_as_claims()
    {
        var local = Task("R1");
        local.Parallel = true;
        local.ConflictsWith = [TaskId.Parse("R2")];
        Assert.Equal(
            ["conflicts", "parallel"],
            TaskExport.Diff([local], [Task("R1")], true).Actions[0].Fields
        );
    }

    [Fact]
    public void The_report_reads_like_a_plan()
    {
        var report = new ExportReport(
            "github",
            "me/app",
            true,
            [new ExportAction(TaskId.Parse("R1"), ExportKind.Update, ["title"], "https://x/1")],
            2,
            1
        );
        Assert.Equal(
            "github (me/app): 0 create, 1 update, 0 close, 0 reopen, 2 unchanged, 1 finished not exported (dry run: nothing sent)\nupdate R1  (title)  https://x/1\n",
            TaskText.Export(report)
        );
        Assert.Equal(
            """{"backend":"github","target":"me/app","dry_run":true,"actions":[{"id":"R1","kind":"update","fields":["title"],"url":"https://x/1"}],"unchanged":2,"skipped":1}""",
            TaskJson.Render(w => TaskJson.Write(w, report), indented: false)
        );
    }

    [Fact]
    public async Task Open_refuses_what_it_cannot_target()
    {
        using var fake = new FakeService();
        var config = new TasksSection();
        var e = await Assert.ThrowsAsync<TaskException>(() =>
            TaskExport.OpenAsync(config, "github.com/me/app", "linear", fake.Context, ct: Ct)
        );
        Assert.Equal(
            "linear export: set [tasks.export.linear] team to the team's short key",
            e.Message
        );
        config.Export.Linear.Team = "ENG";
        e = await Assert.ThrowsAsync<TaskException>(() =>
            TaskExport.OpenAsync(config, "github.com/me/app", "linear", fake.Context, ct: Ct)
        );
        Assert.Equal("linear: no token; set LINEAR_API_KEY", e.Message);
        e = await Assert.ThrowsAsync<TaskException>(() =>
            TaskExport.OpenAsync(config, "github.com/me/app", "jira", fake.Context, ct: Ct)
        );
        Assert.Equal("rtok task export: unknown backend \"jira\"; use github or linear", e.Message);
        Assert.Equal(
            "rtok task export mirrors the disk adapter, but [tasks] adapter is \"github\"",
            Assert.Throws<TaskException>(() => TaskExport.RequireDisk("github")).Message
        );
        TaskExport.RequireDisk("disk");
    }
}
