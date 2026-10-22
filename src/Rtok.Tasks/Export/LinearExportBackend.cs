using System.Text.Json;
using Rtok.Tasks.Remote;

namespace Rtok.Tasks.Export;

/// <summary>
/// The Linear mirror: one GraphQL endpoint, a personal API key and the same label vocabulary
/// as the GitHub adapter, so a task reads back from a Linear issue the way it does from a
/// GitHub one. The status is the team's workflow state, picked by its type.
/// </summary>
internal sealed class LinearExportBackend : IExportBackend
{
    /// <summary>Linear's API.</summary>
    public const string DefaultUrl = "https://api.linear.app/graphql";

    /// <summary>
    /// Issues per page. One query may cost 10,000 complexity points and a connection costs its
    /// page size times its children, so 50 issues with 50 labels each stay near 3,000.
    /// </summary>
    private const int Page = 50;

    private const string TeamQuery =
        "query Team($key: String!) { teams(filter: {key: {eq: $key}}) { nodes { id states(first: 100) { nodes { id type position } } } } }";
    private const string LabelsQuery =
        "query Labels($team: ID!, $after: String) { issueLabels(first: 100, after: $after, filter: {or: [{team: {id: {eq: $team}}}, {team: {null: true}}]}) { nodes { id name } pageInfo { hasNextPage endCursor } } }";

    // Archived issues stay in the listing, or a re-run would create their task a second time.
    private const string IssuesQuery =
        "query Issues($key: String!, $label: String!, $after: String, $first: Int!) { issues(first: $first, after: $after, includeArchived: true, filter: {team: {key: {eq: $key}}, labels: {name: {eq: $label}}}) { nodes { id number url title description state { type } labels(first: 50) { nodes { name } } } pageInfo { hasNextPage endCursor } } }";
    private const string LabelCreate =
        "mutation LabelCreate($input: IssueLabelCreateInput!) { issueLabelCreate(input: $input) { success issueLabel { id name } } }";
    private const string IssueCreate =
        "mutation IssueCreate($input: IssueCreateInput!) { issueCreate(input: $input) { success issue { id number url } } }";
    private const string IssueUpdate =
        "mutation IssueUpdate($id: String!, $input: IssueUpdateInput!) { issueUpdate(id: $id, input: $input) { success issue { id number url } } }";

    private readonly RemoteClient _client;
    private readonly string _team;
    private readonly Dictionary<TaskId, (string Uuid, List<string> Labels)> _issues = [];
    private (string Id, List<(string Id, string Type, double Position)> States)? _teamInfo;

    /// <summary>Label name to id, the team's and the workspace's; filled on the first write.</summary>
    private Dictionary<string, string>? _labels;

    private LinearExportBackend(RemoteClient client, string team)
    {
        _client = client;
        _team = team;
    }

    public string Name => "linear";

    public string Target => _team;

    public bool KeepsClaims => true;

    /// <summary>Opens the backend.</summary>
    /// <param name="url">The GraphQL endpoint.</param>
    /// <param name="team">The team's short key.</param>
    /// <param name="key">A personal API key.</param>
    /// <param name="context">HTTP and clock.</param>
    public static LinearExportBackend Open(
        string url,
        string team,
        string key,
        RemoteContext context
    ) =>
        new(
            new RemoteClient(
                context.Http,
                "linear",
                url,
                request =>
                {
                    // A personal key goes bare; Bearer is for OAuth tokens.
                    request.Headers.TryAddWithoutValidation("Authorization", key);
                    request.Headers.UserAgent.ParseAdd(context.UserAgent);
                },
                context.Clock,
                context.WriteGap
            ),
            team
        );

    public async Task<IReadOnlyList<PlanTask>> RemoteAsync(CancellationToken ct = default)
    {
        var tasks = new List<PlanTask>();
        _issues.Clear();
        string? after = null;
        do
        {
            var data = await QueryAsync(
                    IssuesQuery,
                    w =>
                    {
                        w.WriteString("key", _team);
                        w.WriteString("label", RemoteLabels.Rtok);
                        Cursor(w, after);
                        w.WriteNumber("first", Page);
                    },
                    ct
                )
                .ConfigureAwait(false);
            var issues = RemoteJson.Get(data, "issues");
            foreach (var issue in RemoteJson.Get(issues, "nodes").EnumerateArray())
            {
                var names = RemoteJson
                    .Get(RemoteJson.Get(issue, "labels"), "nodes")
                    .EnumerateArray()
                    .Select(l => RemoteJson.Str(l, "name"))
                    .ToList();
                // An issue with rtok's label but no id label is not one of ours to touch.
                if (RemoteLabels.IdOf(names) is not { } id)
                {
                    continue;
                }
                var uuid = RemoteJson.Str(issue, "id");
                var task = new PlanTask
                {
                    Id = id,
                    Title = RemoteLabels.StripTitle(id, RemoteJson.Str(issue, "title")),
                    Description = RemoteJson.OptStr(issue, "description") ?? "",
                    Status = StatusOf(RemoteJson.Str(RemoteJson.Get(issue, "state"), "type")),
                    Parent = id.Parent,
                    External = new ExternalRef(
                        "linear",
                        (long)Number(issue, "number"),
                        RemoteJson.Str(issue, "url"),
                        uuid
                    ),
                };
                RemoteLabels.Read(names, task);
                _issues.TryAdd(id, (uuid, names));
                tasks.Add(task);
            }
            after = NextCursor(issues);
        } while (after is not null);
        return tasks;
    }

    public async Task<string?> ApplyAsync(
        ExportAction action,
        PlanTask task,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(task);
        var (team, state) = await TeamAndStateAsync(task.Status, ct).ConfigureAwait(false);
        var create = action.Kind == ExportKind.Create;
        var held = _issues.GetValueOrDefault(task.Id);
        if (!create && held.Uuid is null)
        {
            throw new TaskException($"linear: no issue for task {task.Id}");
        }
        IEnumerable<string> existing = create
            ? [RemoteLabels.Rtok, RemoteLabels.IdLabel(task.Id)]
            : held.Labels;
        // Merge keeps a person's own labels and rewrites the ones rtok owns.
        var labels = RemoteLabels.Merge(existing, task, Name);
        var input = new IssueInput(
            RemoteLabels.Title(task.Id, task.Title),
            task.Description,
            await LabelIdsAsync(labels, team, ct).ConfigureAwait(false),
            create ? team : null,
            // On update the state moves only when the status differs: an issue a person
            // parked in another column of the same type stays there.
            create || action.Fields.Contains("status", StringComparer.Ordinal)
                ? state
                : null,
            create ? ParentUuid(task.Id) : null
        );
        var (uuid, url) = await WriteAsync(create ? null : held.Uuid, input, ct)
            .ConfigureAwait(false);
        _issues[task.Id] = (uuid, labels);
        return url;
    }

    private string? ParentUuid(TaskId id) =>
        id.Parent is not { } parent
            ? null
            : _issues.GetValueOrDefault(parent).Uuid
                ?? throw new TaskException($"linear: the parent {parent} of {id} has no issue");

    /// <summary>Creates an issue, or updates the one <paramref name="uuid"/> names.</summary>
    private async Task<(string Uuid, string Url)> WriteAsync(
        string? uuid,
        IssueInput input,
        CancellationToken ct
    )
    {
        var data = await QueryAsync(
                uuid is null ? IssueCreate : IssueUpdate,
                w =>
                {
                    if (uuid is not null)
                    {
                        w.WriteString("id", uuid);
                    }
                    input.Write(w);
                },
                ct
            )
            .ConfigureAwait(false);
        var payload = RemoteJson.Get(data, uuid is null ? "issueCreate" : "issueUpdate");
        if (
            RemoteJson.Find(payload, "success") is not { ValueKind: JsonValueKind.True }
            || RemoteJson.Find(payload, "issue") is not { ValueKind: JsonValueKind.Object } written
        )
        {
            throw new TaskException($"linear: POST {_client.Base}: the write was not accepted");
        }
        return (RemoteJson.Str(written, "id"), RemoteJson.Str(written, "url"));
    }

    private static TaskState StatusOf(string type) =>
        type switch
        {
            "started" => TaskState.InProgress,
            "completed" => TaskState.Done,
            "canceled" or "duplicate" => TaskState.Closed,
            _ => TaskState.Open,
        };

    /// <summary>The team's id and the state a status maps to: of the wanted types in order, the lowest position.</summary>
    private async Task<(string Team, string State)> TeamAndStateAsync(
        TaskState status,
        CancellationToken ct
    )
    {
        if (_teamInfo is null)
        {
            var data = await QueryAsync(TeamQuery, w => w.WriteString("key", _team), ct)
                .ConfigureAwait(false);
            var node = RemoteJson
                .Get(RemoteJson.Get(data, "teams"), "nodes")
                .EnumerateArray()
                .FirstOrDefault()
                is { ValueKind: JsonValueKind.Object } found
                ? found
                : throw new TaskException($"linear: no team with key \"{_team}\"");
            _teamInfo = (
                RemoteJson.Str(node, "id"),
                [
                    .. RemoteJson
                        .Get(RemoteJson.Get(node, "states"), "nodes")
                        .EnumerateArray()
                        .Select(s =>
                            (
                                RemoteJson.Str(s, "id"),
                                RemoteJson.Str(s, "type"),
                                Number(s, "position")
                            )
                        ),
                ]
            );
        }
        var (teamId, states) = _teamInfo.Value;
        // A team with no unstarted state keeps new work in its backlog.
        string[] wanted = status switch
        {
            TaskState.Open => ["unstarted", "backlog"],
            TaskState.InProgress => ["started"],
            TaskState.Done => ["completed"],
            _ => ["canceled"],
        };
        foreach (var type in wanted)
        {
            var match = states
                .Where(s => string.Equals(s.Type, type, StringComparison.Ordinal))
                .OrderBy(s => s.Position)
                .Select(s => s.Id)
                .FirstOrDefault();
            if (match is not null)
            {
                return (teamId, match);
            }
        }
        throw new TaskException(
            $"linear: team {_team} has no workflow state of type {string.Join(" or ", wanted)}"
        );
    }

    /// <summary>The ids of labels, creating the ones the team lacks: a label must exist before an issue names it.</summary>
    private async Task<List<string>> LabelIdsAsync(
        List<string> names,
        string team,
        CancellationToken ct
    )
    {
        if (_labels is null)
        {
            var known = new Dictionary<string, string>(StringComparer.Ordinal);
            string? after = null;
            do
            {
                var data = await QueryAsync(
                        LabelsQuery,
                        w =>
                        {
                            w.WriteString("team", team);
                            Cursor(w, after);
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                var page = RemoteJson.Get(data, "issueLabels");
                foreach (var label in RemoteJson.Get(page, "nodes").EnumerateArray())
                {
                    known[RemoteJson.Str(label, "name")] = RemoteJson.Str(label, "id");
                }
                after = NextCursor(page);
            } while (after is not null);
            _labels = known;
        }
        var ids = new List<string>();
        foreach (var name in names)
        {
            if (!_labels.TryGetValue(name, out var id))
            {
                var data = await QueryAsync(
                        LabelCreate,
                        w =>
                        {
                            w.WriteStartObject("input");
                            w.WriteString("name", name);
                            w.WriteString("teamId", team);
                            w.WriteEndObject();
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                var made = RemoteJson.Get(data, "issueLabelCreate");
                if (
                    RemoteJson.Find(made, "success") is not { ValueKind: JsonValueKind.True }
                    || RemoteJson.Find(made, "issueLabel")
                        is not { ValueKind: JsonValueKind.Object } label
                )
                {
                    throw new TaskException(
                        $"linear: POST {_client.Base}: label \"{name}\" was not created"
                    );
                }
                id = RemoteJson.Str(label, "id");
                _labels[name] = id;
            }
            ids.Add(id);
        }
        return ids;
    }

    /// <summary>One query or mutation. A GraphQL failure can come with HTTP 200, so <c>errors</c> is read before <c>data</c>.</summary>
    private async Task<JsonElement> QueryAsync(
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
        var reply = await _client
            .SendAsync(HttpMethod.Post, _client.Base, body, ct)
            .ConfigureAwait(false);
        if (reply.ValueKind != JsonValueKind.Object)
        {
            throw new TaskException($"linear: POST {_client.Base}: the answer has no data");
        }
        if (
            RemoteJson.Find(reply, "errors") is { ValueKind: JsonValueKind.Array } errors
            && errors.GetArrayLength() > 0
        )
        {
            var messages = errors
                .EnumerateArray()
                .Select(e => RemoteJson.OptStr(e, "message") ?? "");
            throw new TaskException($"linear: POST {_client.Base}: {string.Join("; ", messages)}");
        }
        return RemoteJson.Find(reply, "data") is { ValueKind: JsonValueKind.Object } data
            ? data
            : throw new TaskException($"linear: POST {_client.Base}: the answer has no data");
    }

    private static double Number(JsonElement e, string name) =>
        RemoteJson.Get(e, name) is { ValueKind: JsonValueKind.Number } v
            ? v.GetDouble()
            : throw new TaskException($"unexpected JSON: no `{name}`");

    private static void Cursor(Utf8JsonWriter w, string? after)
    {
        if (after is null)
        {
            w.WriteNull("after");
        }
        else
        {
            w.WriteString("after", after);
        }
    }

    private static string? NextCursor(JsonElement page) =>
        RemoteJson.Find(page, "pageInfo") is { } info
        && RemoteJson.Find(info, "hasNextPage") is { ValueKind: JsonValueKind.True }
            ? RemoteJson.OptStr(info, "endCursor")
            : null;

    /// <summary>An <c>IssueCreateInput</c> or <c>IssueUpdateInput</c>; null fields are left out.</summary>
    private sealed record IssueInput(
        string Title,
        string Description,
        List<string> LabelIds,
        string? TeamId,
        string? StateId,
        string? ParentId
    )
    {
        public void Write(Utf8JsonWriter w)
        {
            w.WriteStartObject("input");
            w.WriteString("title", Title);
            w.WriteString("description", Description);
            w.WriteStartArray("labelIds");
            foreach (var id in LabelIds)
            {
                w.WriteStringValue(id);
            }
            w.WriteEndArray();
            Optional(w, "teamId", TeamId);
            Optional(w, "stateId", StateId);
            Optional(w, "parentId", ParentId);
            w.WriteEndObject();
        }

        private static void Optional(Utf8JsonWriter w, string name, string? value)
        {
            if (value is not null)
            {
                w.WriteString(name, value);
            }
        }
    }
}
