using Rtok.Tasks.GitLab;

namespace Rtok.Tasks.Tests;

public sealed class GitLabTaskAdapterTests : IDisposable
{
    // WireMock matches the decoded path; Sent_urls_keep_the_project_encoded checks the wire.
    private const string Issues = "/projects/me/app/issues";
    private readonly FakeService _fake = new();

    public void Dispose() => _fake.Dispose();

    private Task<GitLabTaskAdapter> OpenAsync() =>
        GitLabTaskAdapter.OpenAsync(
            GitLabTaskAdapter.DefaultUrl,
            "",
            "gitlab.com/me/app",
            _fake.Context,
            _fake.Url,
            Ct
        );

    private void Lookup(string label, params string[] issues) =>
        _fake.On(
            "GET",
            Issues,
            $"[{string.Join(',', issues)}]",
            query: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["labels"] = label,
                ["state"] = "all",
            }
        );

    private static TaskId Id(string s) => TaskId.Parse(s);

    [Fact]
    public async Task Create_opens_a_labelled_issue_and_links_its_parent()
    {
        Lookup("rtok:A2.1");
        Lookup("rtok:A2", GitLabJson.Issue(2, "A2. Parent", ["rtok", "rtok:A2"]));
        _fake.On(
            "POST",
            Issues,
            GitLabJson.Issue(7, "A2.1. Leaf", ["rtok", "rtok:A2.1"]),
            status: 201
        );
        _fake.On("POST", $"{Issues}/2/links", "{}", status: 201);
        var gitlab = await OpenAsync();

        var task = await gitlab.CreateAsync(Id("A2.1"), new NewPlanTask("Leaf", "Why."), Ct);

        Assert.Equal(
            [
                """{"title":"A2.1. Leaf","description":"Why.","labels":"rtok,rtok:A2.1"}""",
                """{"target_project_id":42,"target_issue_iid":7,"link_type":"relates_to"}""",
            ],
            _fake.Writes
        );
        Assert.Equal(
            new ExternalRef("gitlab", 7, "https://gitlab.com/me/app/-/issues/7", null),
            task.External
        );
        Assert.Equal((1_791_367_200, 1_791_367_200), (task.CreatedAt, task.UpdatedAt));
        Assert.Empty(_fake.Warnings);
    }

    [Fact]
    public async Task A_failed_link_still_returns_the_task()
    {
        Lookup("rtok:A2.1");
        Lookup("rtok:A2", GitLabJson.Issue(2, "A2. Parent", ["rtok", "rtok:A2"]));
        _fake.On(
            "POST",
            Issues,
            GitLabJson.Issue(7, "A2.1. Leaf", ["rtok", "rtok:A2.1"]),
            status: 201
        );
        _fake.On("POST", $"{Issues}/2/links", """{"message":"403 Forbidden"}""", status: 403);
        var gitlab = await OpenAsync();

        var task = await gitlab.CreateAsync(Id("A2.1"), new NewPlanTask("Leaf"), Ct);

        Assert.Equal(Id("A2.1"), task.Id);
        Assert.Single(_fake.Warnings);
    }

    [Fact]
    public async Task Create_refuses_a_taken_id_a_missing_parent_and_a_dropped_label()
    {
        Lookup("rtok:A1", GitLabJson.Issue(1, "A1. Old", ["rtok", "rtok:A1"]));
        Lookup("rtok:A3.1");
        Lookup("rtok:A3");
        Lookup("rtok:A4");
        _fake.On("POST", Issues, GitLabJson.Issue(4, "A4. New", []), status: 201);
        var gitlab = await OpenAsync();

        await Assert.ThrowsAsync<TaskTakenException>(() =>
            gitlab.CreateAsync(Id("A1"), new NewPlanTask("x"), Ct)
        );
        var orphan = await Assert.ThrowsAsync<TaskException>(() =>
            gitlab.CreateAsync(Id("A3.1"), new NewPlanTask("x"), Ct)
        );
        var dropped = await Assert.ThrowsAsync<TaskException>(() =>
            gitlab.CreateAsync(Id("A4"), new NewPlanTask("New"), Ct)
        );

        Assert.Equal("gitlab tasks: no parent task A3 in me/app", orphan.Message);
        Assert.Equal(
            "gitlab tasks: https://gitlab.com/me/app/-/issues/4 was created without the rtok:A4 label (labels need more than the Guest role in me/app); label or close it by hand",
            dropped.Message
        );
    }

    [Fact]
    public async Task List_tells_done_from_wont_do()
    {
        _fake.On(
            "GET",
            Issues,
            $"[{string.Join(
                ',',
                GitLabJson.Issue(1, "A1. One", ["rtok", "rtok:A1"]),
                GitLabJson.Issue(2, "A2. Two", ["rtok", "rtok:A2", "Status::In-Progress"]),
                GitLabJson.Issue(3, "A3. Three", ["rtok", "rtok:A3"], "closed"),
                GitLabJson.Issue(4, "A4. Four", ["rtok", "rtok:A4", "status::wont-do"], "closed"),
                GitLabJson.Issue(5, "Stray", ["rtok"])
            )}]",
            query: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["labels"] = "rtok",
                ["state"] = "all",
            }
        );
        var gitlab = await OpenAsync();

        var tasks = await gitlab.ListAsync(new TaskFilter { All = true }, Ct);

        Assert.Equal(
            ["A1 Open", "A2 InProgress", "A3 Done", "A4 Closed"],
            tasks.Select(t => $"{t.Id} {t.Status}"),
            StringComparer.Ordinal
        );
        Assert.Empty(await gitlab.UnlabelledAsync(Ct));
    }

    [Fact]
    public async Task Status_writes_swap_the_status_labels_and_move_the_state()
    {
        Lookup("rtok:A1", GitLabJson.Issue(1, "A1. One", ["rtok", "rtok:A1"]));
        Lookup(
            "rtok:A2",
            GitLabJson.Issue(2, "A2. Two", ["rtok", "rtok:A2", "status::done"], "closed")
        );
        _fake.On("PUT", $"{Issues}/1", GitLabJson.Issue(1, "A1. One", ["rtok", "rtok:A1"]));
        _fake.On("PUT", $"{Issues}/2", GitLabJson.Issue(2, "A2. Two", ["rtok", "rtok:A2"]));
        var gitlab = await OpenAsync();

        await gitlab.WriteStatusAsync(Id("A1"), TaskState.InProgress, Ct);
        await gitlab.WriteStatusAsync(Id("A1"), TaskState.Done, Ct);
        await gitlab.WriteStatusAsync(Id("A2"), TaskState.Open, Ct);

        Assert.Equal(
            [
                """{"add_labels":"status::in-progress","remove_labels":"status::done,status::wont-do"}""",
                """{"add_labels":"status::done","remove_labels":"status::in-progress,status::wont-do","state_event":"close"}""",
                """{"remove_labels":"status::in-progress,status::done,status::wont-do","state_event":"reopen"}""",
            ],
            _fake.Writes
        );
    }

    [Fact]
    public async Task Save_sends_only_what_differs()
    {
        Lookup("rtok:A1", GitLabJson.Issue(1, "A1. One", ["rtok", "rtok:A1", "bug"]));
        Lookup(
            "rtok:A5",
            GitLabJson.Issue(
                5,
                "A5. Five",
                ["rtok", "rtok:A5", "rtok:conflicts:A9", "rtok:parallel"]
            )
        );
        _fake.On(
            "PUT",
            $"{Issues}/1",
            GitLabJson.Issue(
                1,
                "A1. One",
                ["rtok", "rtok:A1", "bug", "status::in-progress", "rtok:owner:agent-1"]
            )
        );
        _fake.On(
            "PUT",
            $"{Issues}/5",
            GitLabJson.Issue(
                5,
                "A5. Five",
                ["rtok", "rtok:A5", "rtok:conflicts:A2", "rtok:conflicts:A3"]
            )
        );
        var gitlab = await OpenAsync();

        var one = (await gitlab.FindAsync(Id("A1"), Ct))!;
        await gitlab.SaveAsync(one, Ct);
        Assert.Empty(_fake.Writes);
        one.Status = TaskState.InProgress;
        one.Assignee = "agent-1";
        var saved = await gitlab.SaveAsync(one, Ct);
        var five = (await gitlab.FindAsync(Id("A5"), Ct))!;
        five.ConflictsWith = [Id("A2"), Id("A3")];
        five.Parallel = false;
        await gitlab.SaveAsync(five, Ct);

        Assert.Equal(
            [
                """{"add_labels":"status::in-progress,rtok:owner:agent-1"}""",
                """{"remove_labels":"rtok:conflicts:A9,rtok:parallel","add_labels":"rtok:conflicts:A2,rtok:conflicts:A3"}""",
            ],
            _fake.Writes
        );
        Assert.Equal((TaskState.InProgress, "agent-1"), (saved.Status, saved.Assignee));
    }

    [Fact]
    public async Task A_missing_task_is_named_with_the_decoded_project()
    {
        Lookup("rtok:A9");
        var gitlab = await OpenAsync();

        var e = await Assert.ThrowsAsync<TaskException>(() =>
            gitlab.WriteStatusAsync(Id("A9"), TaskState.Done, Ct)
        );

        Assert.Equal("gitlab tasks: no task A9 in me/app", e.Message);
    }

    [Fact]
    public async Task Sent_urls_keep_the_project_encoded()
    {
        Lookup("rtok:A9");
        var gitlab = await OpenAsync();

        await gitlab.FindAsync(Id("A9"), Ct);

        Assert.StartsWith(
            $"{_fake.Url}/projects/me%2Fapp/issues?labels=rtok%3AA9&state=all",
            _fake.Server.LogEntries.Single().RequestMessage!.Url,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData("https://gitlab.com", "", "gitlab.com/me/app", "me%2Fapp")]
    [InlineData("https://gitlab.com", "", "gitlab.com/grp/sub/app", "grp%2Fsub%2Fapp")]
    [InlineData("https://gitlab.com", " /1234/ ", "github.com/x/y", "1234")]
    [InlineData(
        "https://GIT.example.com/gitlab/",
        "",
        "git.example.com:8443/gitlab/me/app",
        "me%2Fapp"
    )]
    [InlineData("https://git.example.com", "", "git.example.com/me/app", "me%2Fapp")]
    [InlineData(
        "https://gitlab.com",
        "",
        "github.com/me/app",
        "gitlab tasks: origin github.com/me/app is not on gitlab.com; set [tasks.gitlab] project"
    )]
    [InlineData(
        "https://gitlab.com",
        "",
        "/home/me/app",
        "gitlab tasks: origin /home/me/app is not on gitlab.com; set [tasks.gitlab] project"
    )]
    [InlineData(
        "https://gitlab.com",
        "app",
        "",
        "gitlab tasks: project \"app\" is not group/name or a numeric id"
    )]
    [InlineData(
        "https://gitlab.com",
        "me/../app",
        "",
        "gitlab tasks: project \"me/../app\" is not group/name or a numeric id"
    )]
    [InlineData(
        "http://gitlab.com",
        "me/app",
        "",
        "gitlab tasks: [tasks.gitlab] url \"http://gitlab.com\" must be a plain https:// base URL"
    )]
    [InlineData(
        "https://gitlab.com/?x=1",
        "me/app",
        "",
        "gitlab tasks: [tasks.gitlab] url \"https://gitlab.com/?x=1\" must be a plain https:// base URL"
    )]
    [InlineData(
        "https://u:p@gitlab.com",
        "me/app",
        "",
        "gitlab tasks: [tasks.gitlab] url \"https://u:p@gitlab.com\" must be a plain https:// base URL"
    )]
    [InlineData(
        "gitlab.com",
        "me/app",
        "",
        "gitlab tasks: [tasks.gitlab] url \"gitlab.com\" must be a plain https:// base URL"
    )]
    public void The_project_comes_from_config_or_the_origin(
        string url,
        string configured,
        string key,
        string expected
    )
    {
        string got;
        try
        {
            got = GitLabTaskAdapter
                .ResolveProject(configured, key, GitLabTaskAdapter.Instance(url))
                .Encoded;
        }
        catch (TaskException e)
        {
            got = e.Message;
        }
        Assert.Equal(expected, got);
    }

    [Fact]
    public async Task The_api_lives_under_the_instance_path_and_glab_is_asked_for_its_host()
    {
        _fake.Env.Clear();
        IReadOnlyList<string>? ran = null;
        var context = _fake.Context with
        {
            Run = (argv, _) =>
            {
                ran = argv;
                return Task.FromResult<string?>(null);
            },
        };

        var e = await Assert.ThrowsAsync<TaskException>(() =>
            GitLabTaskAdapter.OpenAsync(
                "https://git.example.com/gitlab",
                "me/app",
                "",
                context,
                ct: Ct
            )
        );

        Assert.Equal(["glab", "config", "get", "token", "--host", "git.example.com"], ran);
        Assert.Equal(
            "gitlab: no token; set GITLAB_TOKEN or GITLAB_ACCESS_TOKEN or GL_TOKEN or run `glab config get token --host git.example.com`",
            e.Message
        );
    }
}
