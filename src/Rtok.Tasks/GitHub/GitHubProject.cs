using System.Text.Json;
using Rtok.Tasks.Remote;

namespace Rtok.Tasks.GitHub;

/// <summary>
/// Mirrors a task's state into the Status field of a Projects v2 board owned by the repository's
/// owner. Every failure is a warning: the issue write already happened and stands.
/// </summary>
internal sealed class GitHubProject(
    RemoteClient client,
    string owner,
    long number,
    Action<string> warn
)
{
    private const string Resolve =
        "query($owner:String!,$number:Int!){repositoryOwner(login:$owner){... on User{projectV2(number:$number){...P}} ... on Organization{projectV2(number:$number){...P}}}} fragment P on ProjectV2{id field(name:\"Status\"){... on ProjectV2SingleSelectField{id options{id name}}}}";

    private const string Add =
        "mutation($project:ID!,$content:ID!){addProjectV2ItemById(input:{projectId:$project,contentId:$content}){item{id}}}";

    private const string Set =
        "mutation($project:ID!,$item:ID!,$field:ID!,$option:String!){updateProjectV2ItemFieldValue(input:{projectId:$project,itemId:$item,fieldId:$field,value:{singleSelectOptionId:$option}}){projectV2Item{id}}}";

    private Board? _board;

    /// <summary>Adds the issue to the board and sets its Status; warns instead of failing.</summary>
    public async Task SyncAsync(TaskId id, string? nodeId, TaskState state, CancellationToken ct)
    {
        if (number == 0)
        {
            return;
        }
        try
        {
            if (nodeId is null)
            {
                throw new TaskException("the issue has no node id");
            }
            var board = _board ??= await ResolveAsync(ct).ConfigureAwait(false);
            var option = Option(board, state);
            var added = await GraphQlAsync(
                    Add,
                    w =>
                    {
                        w.WriteString("project", board.Project);
                        w.WriteString("content", nodeId);
                    },
                    ct
                )
                .ConfigureAwait(false);
            var item = RemoteJson.Str(
                RemoteJson.Get(RemoteJson.Get(added, "addProjectV2ItemById"), "item"),
                "id"
            );
            await GraphQlAsync(
                    Set,
                    w =>
                    {
                        w.WriteString("project", board.Project);
                        w.WriteString("item", item);
                        w.WriteString("field", board.Field);
                        w.WriteString("option", option);
                    },
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (TaskException e)
        {
            warn(
                $"github tasks: project #{number}: Status of {id} not updated, the issue is: {e.Message}"
            );
        }
    }

    private async Task<Board> ResolveAsync(CancellationToken ct)
    {
        var data = await GraphQlAsync(
                Resolve,
                w =>
                {
                    w.WriteString("owner", owner);
                    w.WriteNumber("number", number);
                },
                ct
            )
            .ConfigureAwait(false);
        var project =
            RemoteJson.Find(data, "repositoryOwner") is { } who
            && RemoteJson.Find(who, "projectV2") is { } p
                ? p
                : throw new TaskException($"{owner} has no project #{number}");
        if (
            RemoteJson.Find(project, "field") is not { } field
            || RemoteJson.OptStr(field, "id") is not { } fieldId
        )
        {
            throw new TaskException("the project has no single-select Status field");
        }
        var options = new List<(string Id, string Name)>();
        if (RemoteJson.Find(field, "options") is { ValueKind: JsonValueKind.Array } list)
        {
            foreach (var o in list.EnumerateArray())
            {
                options.Add((RemoteJson.Str(o, "id"), RemoteJson.Str(o, "name")));
            }
        }
        return new Board(RemoteJson.Str(project, "id"), fieldId, options);
    }

    private static string Option(Board board, TaskState state)
    {
        string[] names = state switch
        {
            TaskState.Open => ["Todo", "To do"],
            TaskState.InProgress => ["In Progress"],
            _ => ["Done"],
        };
        foreach (var name in names)
        {
            foreach (var (optionId, optionName) in board.Options)
            {
                if (string.Equals(optionName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return optionId;
                }
            }
        }
        throw new TaskException($"the Status field has no \"{names[0]}\" option");
    }

    private async Task<JsonElement> GraphQlAsync(
        string query,
        Action<Utf8JsonWriter> variables,
        CancellationToken ct
    )
    {
        var body = TaskJson.Render(
            w =>
            {
                w.WriteStartObject();
                w.WriteString("query", query);
                w.WriteStartObject("variables");
                variables(w);
                w.WriteEndObject();
                w.WriteEndObject();
            },
            indented: false
        );
        var answer = await client
            .SendAsync(HttpMethod.Post, $"{client.Base}/graphql", body, ct)
            .ConfigureAwait(false);
        if (
            RemoteJson.Find(answer, "errors") is { ValueKind: JsonValueKind.Array } errors
            && errors.GetArrayLength() > 0
        )
        {
            var messages = errors
                .EnumerateArray()
                .Select(e => RemoteJson.OptStr(e, "message") ?? e.ToString());
            throw new TaskException($"GraphQL: {string.Join("; ", messages)}");
        }
        return RemoteJson.Find(answer, "data")
            ?? throw new TaskException("GraphQL: the answer has no data");
    }

    private sealed record Board(
        string Project,
        string Field,
        IReadOnlyList<(string Id, string Name)> Options
    );
}
