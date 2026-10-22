using System.Globalization;
using System.Text.Json;
using Rtok.Config;
using Rtok.Tasks.Export;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Rtok.Tasks.Tests;

public sealed class LinearExportTests : IDisposable
{
    private readonly FakeService _fake = new();

    public LinearExportTests() => _fake.Env["LINEAR_API_KEY"] = "lin_api_test";

    public void Dispose() => _fake.Dispose();

    private Task<IExportBackend> OpenAsync()
    {
        var config = new TasksSection();
        config.Export.Linear.Team = "ENG";
        return TaskExport.OpenAsync(
            config,
            "github.com/me/app",
            "linear",
            _fake.Context,
            linearUrl: $"{_fake.Url}/graphql",
            ct: Ct
        );
    }

    private static PlanTask Local(string id, string title, TaskState status = TaskState.Open) =>
        new()
        {
            Id = TaskId.Parse(id),
            Title = title,
            Description = "Why.",
            Status = status,
            Parent = TaskId.Parse(id).Parent,
        };

    private static string Summary(ExportReport r) =>
        string.Join(", ", r.Actions.Select(a => $"{a.Kind.Word()} {a.Id}"));

    private static string Data(string inner) => $"{{\"data\":{inner}}}";

    /// <summary>An issue as the Issues query returns it.</summary>
    private static string Issue(
        int n,
        string task,
        string title,
        string state,
        params string[] extra
    )
    {
        var labels = string.Join(
            ',',
            new[] { "rtok", $"rtok:{task}" }.Concat(extra).Select(l => $"{{\"name\":\"{l}\"}}")
        );
        var number = n.ToString(CultureInfo.InvariantCulture);
        return $"{{\"id\":\"uuid-{number}\",\"number\":{number},\"url\":\"https://linear.app/acme/issue/ENG-{number}\",\"title\":\"{task}. {title}\",\"description\":\"Why.\",\"state\":{{\"type\":\"{state}\"}},\"labels\":{{\"nodes\":[{labels}]}}}}";
    }

    private static string Issues(params string[] nodes) =>
        Data(
            $"{{\"issues\":{{\"nodes\":[{string.Join(',', nodes)}],\"pageInfo\":{{\"hasNextPage\":false,\"endCursor\":null}}}}}}"
        );

    private static string Written(string op, int n)
    {
        var number = n.ToString(CultureInfo.InvariantCulture);
        return Data(
            $"{{\"{op}\":{{\"success\":true,\"issue\":{{\"id\":\"uuid-{number}\",\"number\":{number},\"url\":\"https://linear.app/acme/issue/ENG-{number}\"}}}}}}"
        );
    }

    private static string LabelMade(string id, string name) =>
        Data(
            $"{{\"issueLabelCreate\":{{\"success\":true,\"issueLabel\":{{\"id\":\"{id}\",\"name\":\"{name}\"}}}}}}"
        );

    /// <summary>Answers the operation named <paramref name="op"/> when <paramref name="check"/> likes its variables.</summary>
    private void On(string op, Func<JsonElement, bool> check, string answer, int status = 200) =>
        _fake
            .Server.Given(
                Request
                    .Create()
                    .WithPath("/graphql")
                    .UsingPost()
                    .WithHeader("Authorization", "lin_api_test")
                    .WithBody(b => Matches(b, op, check))
            )
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(status)
                    .WithBody(answer)
                    .WithHeader("Content-Type", "application/json")
            );

    private void On(string op, string answer) => On(op, _ => true, answer);

    private static bool Matches(string? body, string op, Func<JsonElement, bool> check)
    {
        if (body is null)
        {
            return false;
        }
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("query")
                .GetString()!
                .Contains(op, StringComparison.Ordinal)
            && check(doc.RootElement.GetProperty("variables"));
    }

    private static bool Is(JsonElement e, string expected, params string[] path) =>
        string.Equals(Str(e, path), expected, StringComparison.Ordinal);

    private static string? Str(JsonElement e, params string[] path)
    {
        foreach (var name in path)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out e))
            {
                return null;
            }
        }
        return e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText();
    }

    /// <summary>The team lookup and the label list every write run starts with.</summary>
    private void Setup()
    {
        On(
            "query Team",
            Data(
                """{"teams":{"nodes":[{"id":"team-1","states":{"nodes":[{"id":"s-backlog","type":"backlog","position":0.0},{"id":"s-todo","type":"unstarted","position":1.0},{"id":"s-todo2","type":"unstarted","position":2.0},{"id":"s-doing","type":"started","position":3.0},{"id":"s-done","type":"completed","position":4.0},{"id":"s-wont","type":"canceled","position":5.0}]}}]}}"""
            )
        );
        On(
            "query Labels",
            Data(
                """{"issueLabels":{"nodes":[{"id":"l-rtok","name":"rtok"},{"id":"l-bug","name":"bug"}],"pageInfo":{"hasNextPage":false,"endCursor":null}}}"""
            )
        );
    }

    private int Calls(string op) =>
        _fake.Calls.Count(c => c.Body?.Contains(op, StringComparison.Ordinal) == true);

    [Fact]
    public async Task A_new_task_becomes_an_issue_and_a_second_run_sends_nothing()
    {
        Setup();
        On("query Issues", Issues());
        On(
            "mutation LabelCreate",
            v => Is(v, """{"name":"rtok:R1","teamId":"team-1"}""", "input"),
            LabelMade("l-r1", "rtok:R1")
        );
        On(
            "mutation IssueCreate",
            v =>
                Is(
                    v,
                    """{"title":"R1. Ship","description":"Why.","labelIds":["l-rtok","l-r1"],"teamId":"team-1","stateId":"s-todo"}""",
                    "input"
                ),
            Written("issueCreate", 7)
        );
        PlanTask[] tasks = [Local("R1", "Ship")];

        var first = await TaskExport.RunAsync(await OpenAsync(), tasks, dryRun: false, Ct);

        Assert.Equal("create R1", Summary(first));
        Assert.Equal("https://linear.app/acme/issue/ENG-7", first.Actions[0].Url);
        Assert.Equal(1, Calls("mutation IssueCreate"));

        _fake.Server.Reset();
        Setup();
        On("query Issues", Issues(Issue(7, "R1", "Ship", "unstarted")));
        var again = await TaskExport.RunAsync(await OpenAsync(), tasks, dryRun: false, Ct);
        Assert.Empty(again.Actions);
        Assert.Equal(1, again.Unchanged);
        Assert.Equal(0, Calls("mutation"));
    }

    /// <summary>The four updates the drift test expects: a retitle, two closes and a reopen.</summary>
    private void OnUpdates()
    {
        // The retitle leaves the state alone: backlog already reads as open.
        On(
            "mutation IssueUpdate",
            v =>
                Is(v, "uuid-1", "id")
                && Is(v, "R1. New title", "input", "title")
                && Str(v, "input", "stateId") is null
                && Is(v, """["l-rtok","l-x","l-bug"]""", "input", "labelIds"),
            Written("issueUpdate", 1)
        );
        On(
            "mutation IssueUpdate",
            v => Is(v, "uuid-2", "id") && Is(v, "s-done", "input", "stateId"),
            Written("issueUpdate", 2)
        );
        On(
            "mutation IssueUpdate",
            v => Is(v, "uuid-3", "id") && Is(v, "s-wont", "input", "stateId"),
            Written("issueUpdate", 3)
        );
        On(
            "mutation IssueUpdate",
            v => Is(v, "uuid-4", "id") && Is(v, "s-todo", "input", "stateId"),
            Written("issueUpdate", 4)
        );
    }

    [Fact]
    public async Task Drift_is_updated_and_finished_tasks_move_to_their_state_type()
    {
        Setup();
        On(
            "query Issues",
            Issues(
                Issue(1, "R1", "Old title", "backlog", "bug"),
                Issue(2, "R2", "Two", "started"),
                Issue(3, "R3", "Three", "unstarted"),
                Issue(4, "R4", "Four", "completed")
            )
        );
        On("mutation LabelCreate", LabelMade("l-x", "x"));
        OnUpdates();

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
                "https://linear.app/acme/issue/ENG-1",
                "https://linear.app/acme/issue/ENG-2",
                "https://linear.app/acme/issue/ENG-3",
                "https://linear.app/acme/issue/ENG-4",
            ],
            report.Actions.Select(a => a.Url!),
            StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task A_subtask_hangs_under_its_parent_and_a_finished_parent_is_created_done()
    {
        Setup();
        On("query Issues", Issues());
        On("mutation LabelCreate", LabelMade("l-x", "x"));
        On(
            "mutation IssueCreate",
            v => Is(v, "R1. Big", "input", "title") && Is(v, "s-done", "input", "stateId"),
            Written("issueCreate", 1)
        );
        On(
            "mutation IssueCreate",
            v =>
                Is(v, "R1.1. Small", "input", "title")
                && Is(v, "uuid-1", "input", "parentId")
                && Is(v, "s-doing", "input", "stateId"),
            Written("issueCreate", 2)
        );

        var report = await TaskExport.RunAsync(
            await OpenAsync(),
            [Local("R1.1", "Small", TaskState.InProgress), Local("R1", "Big", TaskState.Done)],
            dryRun: false,
            Ct
        );

        Assert.Equal("create R1, create R1.1", Summary(report));
        Assert.Equal("https://linear.app/acme/issue/ENG-2", report.Actions[1].Url);
    }

    [Fact]
    public async Task A_dry_run_lists_the_writes_and_sends_none()
    {
        On("query Issues", Issues(Issue(1, "R1", "Old", "unstarted")));

        var report = await TaskExport.RunAsync(
            await OpenAsync(),
            [Local("R1", "New"), Local("R2", "Fresh")],
            dryRun: true,
            Ct
        );

        Assert.Equal("update R1, create R2", Summary(report));
        Assert.Equal(["title"], report.Actions[0].Fields);
        Assert.Equal(0, Calls("mutation") + Calls("query Team"));
    }

    [Fact]
    public async Task A_graphql_error_in_a_200_names_linear_and_the_url()
    {
        On(
            "query Issues",
            """{"errors":[{"message":"Argument Validation Error","path":["issues"]}]}"""
        );

        var e = await Assert.ThrowsAsync<TaskException>(async () =>
            await TaskExport.RunAsync(await OpenAsync(), [], dryRun: false, Ct)
        );

        Assert.Equal(
            $"linear export: listing the existing issues: linear: POST {_fake.Url}/graphql: Argument Validation Error",
            e.Message
        );
    }

    [Fact]
    public async Task A_rate_limit_arrives_as_a_400_and_says_so()
    {
        On(
            "query Issues",
            _ => true,
            """{"errors":[{"message":"Rate limit exceeded","extensions":{"code":"RATELIMITED"}}]}""",
            status: 400
        );

        var e = await Assert.ThrowsAsync<TaskException>(async () =>
            await TaskExport.RunAsync(await OpenAsync(), [], dryRun: false, Ct)
        );

        Assert.Contains("linear: POST ", e.Message, StringComparison.Ordinal);
        Assert.Contains("Rate limit exceeded", e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("lin_api_test", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_team_without_a_state_for_the_status_is_named()
    {
        On("query Issues", Issues());
        On(
            "query Team",
            Data(
                """{"teams":{"nodes":[{"id":"team-1","states":{"nodes":[{"id":"s-done","type":"completed","position":0.0}]}}]}}"""
            )
        );

        var e = await Assert.ThrowsAsync<TaskException>(async () =>
            await TaskExport.RunAsync(await OpenAsync(), [Local("R1", "Go")], dryRun: false, Ct)
        );

        Assert.Equal(
            "linear export: create R1 failed after 0 of 1 writes: linear: team ENG has no workflow state of type unstarted or backlog",
            e.Message
        );
    }
}
