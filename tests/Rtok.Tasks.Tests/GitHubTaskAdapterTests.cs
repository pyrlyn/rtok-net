using Rtok.Tasks.GitHub;

namespace Rtok.Tasks.Tests;

public sealed class GitHubTaskAdapterTests : IDisposable
{
    private const string Issues = "/repos/me/app/issues";
    private readonly FakeService _fake = new();

    public void Dispose() => _fake.Dispose();

    private Task<GitHubTaskAdapter> OpenAsync(long project = 0) =>
        GitHubTaskAdapter.OpenAsync("me/app", project, _fake.Context, _fake.Url, Ct);

    private void Lookup(string label, params string[] issues) =>
        _fake.On(
            "GET",
            Issues,
            GitHubJson.Issues(issues),
            query: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["labels"] = label,
                ["state"] = "all",
            },
            headers: null
        );

    private static TaskId Id(string s) => TaskId.Parse(s);

    [Fact]
    public async Task Create_opens_a_labelled_issue_and_links_the_sub_issue()
    {
        Lookup("rtok:R2.1");
        Lookup("rtok:R2", GitHubJson.Issue(2, "R2. Parent", ["rtok", "rtok:R2"]));
        _fake.On(
            "POST",
            Issues,
            GitHubJson.Issue(9, "R2.1. Ship it", ["rtok", "rtok:R2.1"]),
            status: 201
        );
        _fake.On("POST", $"{Issues}/2/sub_issues", "{}", status: 201);
        var github = await OpenAsync();

        var task = await github.CreateAsync(Id("R2.1"), new NewPlanTask("Ship it ", "Why."), Ct);

        Assert.Equal(
            [
                """{"title":"R2.1. Ship it","body":"Why.","labels":["rtok","rtok:R2.1"]}""",
                """{"sub_issue_id":900}""",
            ],
            _fake.Writes
        );
        Assert.Equal(("Ship it", TaskState.Open, Id("R2")), (task.Title, task.Status, task.Parent));
        Assert.Equal(1_791_367_200, task.CreatedAt);
        Assert.Equal(
            new ExternalRef("github", 9, "https://github.com/me/app/issues/9", "I_9"),
            task.External
        );
        Assert.Empty(_fake.Warnings);
        Assert.All(
            _fake.Server.LogEntries,
            e =>
                Assert.Equal(
                    "Bearer test-token",
                    e.RequestMessage!.Headers!["Authorization"].Single()
                )
        );
    }

    [Fact]
    public async Task A_failed_sub_issue_link_only_warns()
    {
        Lookup("rtok:R2.1");
        Lookup("rtok:R2", GitHubJson.Issue(2, "R2. Parent", ["rtok", "rtok:R2"]));
        _fake.On(
            "POST",
            Issues,
            GitHubJson.Issue(9, "R2.1. Leaf", ["rtok", "rtok:R2.1"]),
            status: 201
        );
        _fake.On("POST", $"{Issues}/2/sub_issues", """{"message":"Not Found"}""", status: 404);
        var github = await OpenAsync();

        var task = await github.CreateAsync(Id("R2.1"), new NewPlanTask("Leaf"), Ct);

        Assert.Equal(Id("R2.1"), task.Id);
        Assert.Contains("Not Found", Assert.Single(_fake.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_refuses_a_taken_id_a_missing_parent_and_a_dropped_label()
    {
        Lookup("rtok:R1", GitHubJson.Issue(1, "R1. Old", ["rtok", "rtok:R1"]));
        Lookup("rtok:R3.1");
        Lookup("rtok:R3");
        Lookup("rtok:R4");
        _fake.On("POST", Issues, GitHubJson.Issue(4, "R4. New", ["rtok"]), status: 201);
        var github = await OpenAsync();

        await Assert.ThrowsAsync<TaskTakenException>(() =>
            github.CreateAsync(Id("R1"), new NewPlanTask("x"), Ct)
        );
        var orphan = await Assert.ThrowsAsync<TaskException>(() =>
            github.CreateAsync(Id("R3.1"), new NewPlanTask("x"), Ct)
        );
        Assert.Equal("github tasks: no parent task R3 in me/app", orphan.Message);
        var dropped = await Assert.ThrowsAsync<TaskException>(() =>
            github.CreateAsync(Id("R4"), new NewPlanTask("New"), Ct)
        );
        Assert.Equal(
            "github tasks: https://github.com/me/app/issues/4 was created without the rtok:R4 label (labels need push access to me/app); label or close it by hand",
            dropped.Message
        );
    }

    [Fact]
    public async Task List_reads_states_and_skips_pull_requests_and_strays()
    {
        _fake.On(
            "GET",
            Issues,
            GitHubJson.Issues(
                GitHubJson.Issue(1, "R1. One", ["rtok", "rtok:R1"]),
                GitHubJson.Issue(
                    2,
                    "Edited by hand",
                    ["rtok", "rtok:R2", "rtok:in-progress", "rtok:owner:a1"]
                ),
                GitHubJson.Issue(3, "R3. Three", ["rtok", "rtok:R3"], "closed", "completed"),
                GitHubJson.Issue(4, "R4. Four", ["rtok", "rtok:R4"], "closed", "not_planned"),
                GitHubJson.Issue(5, "R5. Lost", ["rtok"]),
                GitHubJson.Issue(6, "R6. A PR", ["rtok", "rtok:R6"], pull: true)
            ),
            query: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["labels"] = "rtok",
                ["state"] = "all",
            }
        );
        var github = await OpenAsync();

        var tasks = await github.ListAsync(new TaskFilter { All = true }, Ct);
        var strays = await github.UnlabelledAsync(Ct);

        Assert.Equal(
            ["R1 Open One", "R2 InProgress Edited by hand", "R3 Done Three", "R4 Closed Four"],
            tasks.Select(t => $"{t.Id} {t.Status} {t.Title}"),
            StringComparer.Ordinal
        );
        Assert.Equal("a1", tasks[1].Assignee);
        Assert.Equal(
            new StrayIssue("R5. Lost", "https://github.com/me/app/issues/5", Id("R5")),
            Assert.Single(strays)
        );
    }

    [Fact]
    public async Task Active_lists_ask_only_for_open_issues()
    {
        _fake.On(
            "GET",
            Issues,
            "[]",
            query: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["labels"] = "rtok",
                ["state"] = "open",
            }
        );
        var github = await OpenAsync();

        Assert.Empty(await github.ListAsync(new TaskFilter(), Ct));
        Assert.Single(_fake.Calls);
    }

    [Fact]
    public async Task Status_writes_patch_labels_and_state()
    {
        Lookup("rtok:A1", GitHubJson.Issue(1, "A1. One", ["rtok", "rtok:A1", "bug"]));
        Lookup(
            "rtok:A2",
            GitHubJson.Issue(
                2,
                "A2. Two",
                ["rtok", "rtok:A2", "rtok:in-progress"],
                "closed",
                "completed"
            )
        );
        _fake.On(
            "PATCH",
            $"{Issues}/1",
            GitHubJson.Issue(1, "A1. One", ["rtok", "rtok:A1", "bug", "rtok:in-progress"])
        );
        _fake.On("PATCH", $"{Issues}/2", GitHubJson.Issue(2, "A2. Two", ["rtok", "rtok:A2"]));
        var github = await OpenAsync();

        var one = await github.WriteStatusAsync(Id("A1"), TaskState.InProgress, Ct);
        var two = await github.WriteStatusAsync(Id("A2"), TaskState.Open, Ct);

        Assert.Equal(
            [
                """{"labels":["rtok","rtok:A1","bug","rtok:in-progress"],"state":"open"}""",
                """{"labels":["rtok","rtok:A2"],"state":"open","state_reason":"reopened"}""",
            ],
            _fake.Writes
        );
        Assert.Equal((TaskState.InProgress, TaskState.Open), (one.Status, two.Status));
    }

    [Fact]
    public async Task Done_and_closed_say_why()
    {
        Lookup("rtok:A1", GitHubJson.Issue(1, "A1. One", ["rtok", "rtok:A1"]));
        _fake.On(
            "PATCH",
            $"{Issues}/1",
            GitHubJson.Issue(1, "A1. One", ["rtok", "rtok:A1"], "closed", "completed")
        );
        var github = await OpenAsync();

        await github.WriteStatusAsync(Id("A1"), TaskState.Done, Ct);
        await github.WriteStatusAsync(Id("A1"), TaskState.Closed, Ct);

        Assert.Equal(
            [
                """{"labels":["rtok","rtok:A1"],"state":"closed","state_reason":"completed"}""",
                """{"labels":["rtok","rtok:A1"],"state":"closed","state_reason":"not_planned"}""",
            ],
            _fake.Writes
        );
    }

    [Fact]
    public async Task Save_keeps_foreign_labels_and_rewrites_the_managed_ones()
    {
        Lookup(
            "rtok:A1",
            GitHubJson.Issue(
                1,
                "A1. One",
                ["rtok", "rtok:A1", "rtok:owner:old", "bug", "rtok:p:3", "rtok:parallel"]
            )
        );
        _fake.On(
            "PATCH",
            $"{Issues}/1",
            GitHubJson.Issue(
                1,
                "A1. One",
                [
                    "rtok",
                    "rtok:A1",
                    "bug",
                    "rtok:owner:agent-1",
                    "rtok:needs:A2",
                    "rtok:p:0",
                    "rtok:in-progress",
                ]
            )
        );
        var github = await OpenAsync();
        var task = (await github.FindAsync(Id("A1"), Ct))!;
        Assert.Equal(("old", 3, true), (task.Assignee, task.Priority, task.Parallel));
        task.Assignee = "agent-1";
        task.BlockedBy = [Id("A2")];
        task.Priority = 0;
        task.Parallel = false;
        task.Status = TaskState.InProgress;

        var saved = await github.SaveAsync(task, Ct);

        Assert.Equal(
            """{"labels":["rtok","rtok:A1","bug","rtok:owner:agent-1","rtok:needs:A2","rtok:p:0","rtok:in-progress"],"state":"open"}""",
            Assert.Single(_fake.Writes)
        );
        Assert.Equal(
            ("agent-1", 0, TaskState.InProgress),
            (saved.Assignee, saved.Priority, saved.Status)
        );
        Assert.Equal([Id("A2")], saved.BlockedBy);
    }

    [Fact]
    public async Task Writes_to_a_missing_task_or_a_lost_label_are_refused()
    {
        Lookup("rtok:R9");
        Lookup("rtok:R1", GitHubJson.Issue(1, "R1. One", ["rtok", "rtok:R1"]));
        _fake.On("PATCH", $"{Issues}/1", GitHubJson.Issue(1, "R1. One", ["rtok"]));
        var github = await OpenAsync();

        var missing = await Assert.ThrowsAsync<TaskException>(() =>
            github.WriteStatusAsync(Id("R9"), TaskState.Done, Ct)
        );
        var lost = await Assert.ThrowsAsync<TaskException>(() =>
            github.WriteStatusAsync(Id("R1"), TaskState.Done, Ct)
        );

        Assert.Equal("github tasks: no task R9 in me/app", missing.Message);
        Assert.Equal("github tasks: R1 lost its label", lost.Message);
    }

    [Fact]
    public async Task The_token_comes_from_the_environment_in_order_then_gh()
    {
        _fake.Env.Clear();
        var none = await Assert.ThrowsAsync<TaskException>(() => OpenAsync());
        Assert.Equal(
            "github: no token; set GH_TOKEN or GITHUB_TOKEN or run `gh auth token`",
            none.Message
        );

        IReadOnlyList<string>? ran = null;
        var context = _fake.Context with
        {
            Run = (argv, _) =>
            {
                ran = argv;
                return Task.FromResult<string?>(" from-gh \n");
            },
        };
        _fake.Env["GITHUB_TOKEN"] = "  ";
        _fake.On("GET", Issues, "[]");
        var github = await GitHubTaskAdapter.OpenAsync("me/app", 0, context, _fake.Url, Ct);
        await github.ListAsync(new TaskFilter(), Ct);
        Assert.Equal(["gh", "auth", "token"], ran);
        Assert.Equal(
            "Bearer from-gh",
            _fake.Server.LogEntries.Single().RequestMessage!.Headers!["Authorization"].Single()
        );

        _fake.Env["GITHUB_TOKEN"] = "second";
        _fake.Env["GH_TOKEN"] = "first";
        ran = null;
        await GitHubTaskAdapter.OpenAsync("me/app", 0, context, _fake.Url, Ct);
        Assert.Null(ran);
    }

    [Theory]
    [InlineData("", "github.com/me/app", "me/app")]
    [InlineData(" you/lib ", "gitlab.com/me/app", "you/lib")]
    [InlineData(
        "",
        "gitlab.com/me/app",
        "github tasks: origin gitlab.com/me/app is not on github.com; set [tasks.github] repo"
    )]
    [InlineData("me", "github.com/me/app", "github tasks: repo \"me\" is not owner/name")]
    [InlineData("me/a/b", "", "github tasks: repo \"me/a/b\" is not owner/name")]
    [InlineData("me/..", "", "github tasks: repo \"me/..\" is not owner/name")]
    [InlineData("me/a b", "", "github tasks: repo \"me/a b\" is not owner/name")]
    public void The_repo_comes_from_config_or_the_origin(
        string configured,
        string key,
        string expected
    )
    {
        string got;
        try
        {
            got = GitHubTaskAdapter.ResolveRepo(configured, key);
        }
        catch (TaskException e)
        {
            got = e.Message;
        }
        Assert.Equal(expected, got);
    }
}
