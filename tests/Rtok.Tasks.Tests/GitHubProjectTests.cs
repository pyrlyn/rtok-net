using Rtok.Tasks.GitHub;

namespace Rtok.Tasks.Tests;

public sealed class GitHubProjectTests : IDisposable
{
    private const string Issues = "/repos/me/app/issues";

    private const string Board = """
        {"data":{"repositoryOwner":{"projectV2":{"id":"p1","field":{"id":"f1","options":[
        {"id":"o_todo","name":"To do"},{"id":"o_doing","name":"In progress"},{"id":"o_done","name":"Done"}]}}}}}
        """;

    private readonly FakeService _fake = new();

    public GitHubProjectTests()
    {
        _fake.On(
            "GET",
            Issues,
            GitHubJson.Issues(GitHubJson.Issue(1, "R1. One", ["rtok", "rtok:R1"])),
            query: new Dictionary<string, string>(StringComparer.Ordinal) { ["labels"] = "rtok:R1" }
        );
        _fake.On(
            "PATCH",
            $"{Issues}/1",
            GitHubJson.Issue(1, "R1. One", ["rtok", "rtok:R1"], "closed", "completed")
        );
    }

    public void Dispose() => _fake.Dispose();

    private Task<GitHubTaskAdapter> OpenAsync(long project = 7) =>
        GitHubTaskAdapter.OpenAsync("me/app", project, _fake.Context, _fake.Url, Ct);

    private int GraphQl(string contains) =>
        _fake.Calls.Count(c =>
            string.Equals(c.Path, "/graphql", StringComparison.Ordinal)
            && c.Body?.Contains(contains, StringComparison.Ordinal) == true
        );

    private void Mutations()
    {
        _fake.On(
            "POST",
            "/graphql",
            """{"data":{"addProjectV2ItemById":{"item":{"id":"it1"}}}}""",
            bodyContains: "addProjectV2ItemById"
        );
        _fake.On(
            "POST",
            "/graphql",
            """{"data":{"updateProjectV2ItemFieldValue":{"projectV2Item":{"id":"it1"}}}}""",
            bodyContains: "updateProjectV2ItemFieldValue"
        );
    }

    [Fact]
    public async Task The_board_is_resolved_once_and_every_write_sets_the_status()
    {
        _fake.On("POST", "/graphql", Board, bodyContains: "repositoryOwner");
        Mutations();
        var github = await OpenAsync();

        await github.WriteStatusAsync(TaskId.Parse("R1"), TaskState.Done, Ct);
        await github.WriteStatusAsync(TaskId.Parse("R1"), TaskState.Done, Ct);

        Assert.Equal(1, GraphQl("repositoryOwner"));
        Assert.Equal(2, GraphQl("addProjectV2ItemById"));
        Assert.Equal(2, GraphQl("\"option\":\"o_done\""));
        Assert.Contains(
            "\"variables\":{\"owner\":\"me\",\"number\":7}",
            _fake
                .Calls.First(c => string.Equals(c.Path, "/graphql", StringComparison.Ordinal))
                .Body,
            StringComparison.Ordinal
        );
        Assert.Empty(_fake.Warnings);
    }

    [Fact]
    public async Task Project_zero_never_calls_graphql()
    {
        var github = await OpenAsync(project: 0);

        await github.WriteStatusAsync(TaskId.Parse("R1"), TaskState.Done, Ct);

        Assert.Equal(0, GraphQl(""));
    }

    [Theory]
    [InlineData("""{"data":{"repositoryOwner":{"projectV2":null}}}""", "me has no project #7")]
    [InlineData(
        """{"errors":[{"message":"Your token has not been granted the required scopes"},{"message":"x"}]}""",
        "GraphQL: Your token has not been granted the required scopes; x"
    )]
    [InlineData(
        """{"data":{"repositoryOwner":{"projectV2":{"id":"p1","field":null}}}}""",
        "the project has no single-select Status field"
    )]
    [InlineData(
        """{"data":{"repositoryOwner":{"projectV2":{"id":"p1","field":{"id":"f1","options":[{"id":"a","name":"Todo"}]}}}}}""",
        "the Status field has no \"Done\" option"
    )]
    public async Task A_failing_board_warns_and_the_issue_write_stands(string answer, string reason)
    {
        _fake.On("POST", "/graphql", answer, bodyContains: "repositoryOwner");
        var github = await OpenAsync();

        var task = await github.WriteStatusAsync(TaskId.Parse("R1"), TaskState.Done, Ct);

        Assert.Equal(TaskState.Done, task.Status);
        Assert.Equal(
            $"github tasks: project #7: Status of R1 not updated, the issue is: {reason}",
            Assert.Single(_fake.Warnings)
        );
    }

    [Fact]
    public async Task A_failed_resolve_is_tried_again_on_the_next_write()
    {
        _fake.On(
            "POST",
            "/graphql",
            """{"message":"boom"}""",
            status: 500,
            bodyContains: "repositoryOwner"
        );
        var github = await OpenAsync();

        await github.WriteStatusAsync(TaskId.Parse("R1"), TaskState.Done, Ct);
        await github.WriteStatusAsync(TaskId.Parse("R1"), TaskState.Done, Ct);

        Assert.Equal(2, GraphQl("repositoryOwner"));
        Assert.Equal(2, _fake.Warnings.Count);
        Assert.Contains("500", _fake.Warnings[0], StringComparison.Ordinal);
    }
}
