namespace Rtok.Tasks.Tests;

/// <summary>The store in memory: one lock for all counters, claims ordered by a sequence.</summary>
internal sealed class MemoryTaskStore : ITaskStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(string, string), uint> _counters = [];
    private readonly Dictionary<(string, TaskId), (TaskClaim Claim, long Seq)> _claims = [];
    private long _seq;

    public Dictionary<string, long> Agents { get; } = new(StringComparer.Ordinal);

    public bool FailAgents { get; set; }

    public Task<T> WithCountersAsync<T>(
        string project,
        Func<ITaskCounters, T> work,
        CancellationToken ct = default
    )
    {
        lock (_gate)
        {
            var copy = new Rows(project, new Dictionary<(string, string), uint>(_counters));
            var result = work(copy);
            foreach (var (k, v) in copy.Values)
            {
                _counters[k] = v;
            }
            return Task.FromResult(result);
        }
    }

    public Task UpsertClaimAsync(
        string project,
        TaskId id,
        string agent,
        string title,
        CancellationToken ct = default
    )
    {
        lock (_gate)
        {
            _claims[(project, id)] = (new TaskClaim(id, agent, title, 0), ++_seq);
        }
        return Task.CompletedTask;
    }

    public Task ClearClaimAsync(string project, TaskId id, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _claims.Remove((project, id));
        }
        return Task.CompletedTask;
    }

    public Task<TaskClaim?> LatestClaimAsync(
        string project,
        string agent,
        CancellationToken ct = default
    )
    {
        lock (_gate)
        {
            return Task.FromResult(
                _claims
                    .Where(c =>
                        string.Equals(c.Key.Item1, project, StringComparison.Ordinal)
                        && string.Equals(c.Value.Claim.Agent, agent, StringComparison.Ordinal)
                    )
                    .OrderByDescending(c => c.Value.Seq)
                    .Select(c => c.Value.Claim)
                    .FirstOrDefault()
            );
        }
    }

    public Task<long?> AgentLastSeenAsync(string agent, CancellationToken ct = default) =>
        FailAgents
            ? throw new InvalidOperationException("store is down")
            : Task.FromResult(Agents.TryGetValue(agent, out var seen) ? seen : (long?)null);

    public Task<string?> ResolveAgentAsync(string agent, CancellationToken ct = default) =>
        Task.FromResult(
            Agents.Keys.FirstOrDefault(a =>
                string.Equals(a, agent, StringComparison.Ordinal)
                || a.StartsWith(agent, StringComparison.Ordinal)
            )
        );

    private sealed class Rows(string project, Dictionary<(string, string), uint> values)
        : ITaskCounters
    {
        public Dictionary<(string, string), uint> Values => values;

        public uint Last(string parent) => values.GetValueOrDefault((project, parent));

        public void SetLast(string parent, uint last) => values[(project, parent)] = last;
    }
}
