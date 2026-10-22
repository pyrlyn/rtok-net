using Rtok.Config;
using Rtok.Tasks.Export;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Rtok.Tasks.Tests;

public sealed class GitHubExportTests : IDisposable
{
    private const string Issues = "/repos/me/app/issues";
    private readonly FakeService _fake = new();

    public void Dispose() => _fake.Dispose();

    private Task<IExportBackend> OpenAsync() =>
        TaskExport.OpenAsync(
            new TasksSection(),
            "github.com/me/app",
            "github",
            _fake.Context,
            githubBase: _fake.Url,
            ct: Ct
        );

    private static string Issue(
        long number,
        string task,
        string title,
        string state = "open",
        string? reason = null
    ) =>
        GitHubJson.Issue(
            number,
            $"{task}. {title}",
            ["rtok", $"rtok:{task}"],
            state,
            reason,
            body: "Why."
        );

    private static PlanTask Local(string id, string title, TaskState status = TaskState.Open) =>
        new()
        {
            Id = TaskId.Parse(id),
            Title = title,
            Description = "Why.",
            Status = status,
            Parent = TaskId.Parse(id).Parent,
        };

    private void Lookup(string label, params string[] issues) =>
        _fake.On(
            "GET",
            Issues,
            GitHubJson.Issues(issues),
            query: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["labels"] = label,
                ["state"] = "all",
            }
        );

    private static string Summary(ExportReport r) =>
        string.Join(", ", r.Actions.Select(a => $"{a.Kind.Word()} {a.Id}"));

    [Fact]
    public async Task A_new_task_becomes_an_issue_and_a_second_run_sends_nothing()
    {
        Lookup("rtok");
        Lookup("rtok:R1");
        _fake.On("POST", Issues, Issue(7, "R1", "Ship"), status: 201);
        PlanTask[] tasks = [Local("R1", "Ship")];

        var first = await TaskExport.RunAsync(await OpenAsync(), tasks, dryRun: false, Ct);

        Assert.Equal("create R1", Summary(first));
        Assert.Equal("https://github.com/me/app/issues/7", first.Actions[0].Url);
        Assert.Equal(
            ["""{"title":"R1. Ship","body":"Why.","labels":["rtok","rtok:R1"]}"""],
            _fake.Writes
        );

        Lookup("rtok", Issue(7, "R1", "Ship"));
        var again = await TaskExport.RunAsync(await OpenAsync(), tasks, dryRun: false, Ct);
        Assert.Empty(again.Actions);
        Assert.Equal(1, again.Unchanged);
        Assert.Single(_fake.Writes);
    }

    [Fact]
    public async Task Drift_is_patched_and_finished_tasks_close_with_their_reason()
    {
        Lookup(
            "rtok",
            Issue(1, "R1", "Old title"),
            Issue(2, "R2", "Two"),
            Issue(3, "R3", "Three"),
            Issue(4, "R4", "Four", "closed", "completed")
        );
        Lookup("rtok:R1", Issue(1, "R1", "x"));
        Lookup("rtok:R2", Issue(2, "R2", "x"));
        Lookup("rtok:R3", Issue(3, "R3", "x"));
        Lookup("rtok:R4", Issue(4, "R4", "Four", "closed", "completed"));
        _fake.On("PATCH", $"{Issues}/1", Issue(1, "R1", "New title"));
        _fake.On("PATCH", $"{Issues}/2", Issue(2, "R2", "Two", "closed", "completed"));
        _fake.On("PATCH", $"{Issues}/3", Issue(3, "R3", "Three", "closed", "not_planned"));
        _fake.On("PATCH", $"{Issues}/4", Issue(4, "R4", "Four"));

        var report = await TaskExport.RunAsync(
            await OpenAsync(),
            [
                Local("R1", "New title"),
                Local("R2", "Two", TaskState.Done),
                Local("R3", "Three", TaskState.Closed),
                Local("R4", "Four"),
                Local("R9", "Old history", TaskState.Done),
            ],
            dryRun: false,
            Ct
        );

        Assert.Equal("update R1, close R2, close R3, reopen R4", Summary(report));
        Assert.Equal(1, report.Skipped);
        Assert.Equal(
            [
                """{"title":"R1. New title","body":"Why.","labels":["rtok","rtok:R1"],"state":"open"}""",
                """{"title":"R2. Two","body":"Why.","labels":["rtok","rtok:R2"],"state":"closed","state_reason":"completed"}""",
                """{"title":"R3. Three","body":"Why.","labels":["rtok","rtok:R3"],"state":"closed","state_reason":"not_planned"}""",
                """{"title":"R4. Four","body":"Why.","labels":["rtok","rtok:R4"],"state":"open","state_reason":"reopened"}""",
            ],
            _fake.Writes
        );
    }

    [Fact]
    public async Task A_dry_run_lists_the_writes_and_sends_none()
    {
        Lookup("rtok", Issue(1, "R1", "Old"));

        var report = await TaskExport.RunAsync(
            await OpenAsync(),
            [Local("R1", "New"), Local("R2", "Fresh")],
            dryRun: true,
            Ct
        );

        Assert.True(report.DryRun);
        Assert.Equal("update R1, create R2", Summary(report));
        Assert.Equal(["title"], report.Actions[0].Fields);
        Assert.Empty(_fake.Writes);
    }

    [Fact]
    public async Task An_in_progress_task_is_created_then_labelled()
    {
        Lookup("rtok");
        // The same label lookup is empty before the POST and finds the issue after it.
        var lookup = Request.Create().WithPath(Issues).UsingGet().WithParam("labels", "rtok:R1");
        _fake
            .Server.Given(lookup)
            .InScenario("create")
            .RespondWith(Response.Create().WithBody("[]"));
        _fake
            .Server.Given(Request.Create().WithPath(Issues).UsingPost())
            .InScenario("create")
            .WillSetStateTo("posted")
            .RespondWith(Response.Create().WithStatusCode(201).WithBody(Issue(5, "R1", "Go")));
        _fake
            .Server.Given(lookup)
            .InScenario("create")
            .WhenStateIs("posted")
            .RespondWith(Response.Create().WithBody(GitHubJson.Issues(Issue(5, "R1", "Go"))));
        _fake.On("PATCH", $"{Issues}/5", Issue(5, "R1", "Go"));
        var task = Local("R1", "Go", TaskState.InProgress);
        task.Assignee = "a1";
        task.BlockedBy = [TaskId.Parse("R2")];
        task.Priority = 0;

        var report = await TaskExport.RunAsync(await OpenAsync(), [task], dryRun: false, Ct);

        Assert.Equal("create R1", Summary(report));
        Assert.Equal(
            """{"title":"R1. Go","body":"Why.","labels":["rtok","rtok:R1","rtok:owner:a1","rtok:needs:R2","rtok:p:0","rtok:in-progress"],"state":"open"}""",
            _fake.Writes[1]
        );
    }

    [Fact]
    public async Task A_failed_write_names_the_backend_the_task_and_how_far_it_got()
    {
        Lookup("rtok");
        Lookup("rtok:R1");
        _fake.On("POST", Issues, """{"message":"Validation Failed"}""", status: 422);

        var e = await Assert.ThrowsAsync<TaskException>(async () =>
            await TaskExport.RunAsync(await OpenAsync(), [Local("R1", "Go")], dryRun: false, Ct)
        );

        Assert.StartsWith(
            "github export: create R1 failed after 0 of 1 writes: ",
            e.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(Issues, e.Message, StringComparison.Ordinal);
        Assert.Contains("Validation Failed", e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("test-token", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_listing_says_so()
    {
        _fake.On("GET", Issues, """{"message":"Not Found"}""", status: 404);

        var e = await Assert.ThrowsAsync<TaskException>(async () =>
            await TaskExport.RunAsync(await OpenAsync(), [Local("R1", "Go")], dryRun: false, Ct)
        );

        Assert.StartsWith(
            "github export: listing the existing issues: github: GET ",
            e.Message,
            StringComparison.Ordinal
        );
    }
}
