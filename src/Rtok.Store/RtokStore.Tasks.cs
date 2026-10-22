using Microsoft.EntityFrameworkCore;
using Rtok.Store.Model;

namespace Rtok.Store;

public sealed partial class RtokStore
{
    /// <summary>Runs <paramref name="work"/> in one immediate transaction over a project's task
    /// counters, so two processes never hand out the same number. An exception rolls back.</summary>
    public T WithTaskCounters<T>(string project, Func<TaskCounters, T> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        using var scope = EnterWrite();
        var result = work(new TaskCounters(scope.Db, project));
        scope.Commit();
        return result;
    }

    /// <summary>Records <paramref name="agent"/>'s claim of a task as its newest one: a
    /// re-claim moves to the front and takes the new title.</summary>
    public void UpsertTaskClaim(string project, string task, string agent, string title)
    {
        using var scope = EnterWrite();
        var db = scope.Db;
        var seq = (db.TaskClaims.Max(c => (long?)c.Seq) ?? 0) + 1;
        var (p, t) = (project, task);
        var row = db.TaskClaims.FirstOrDefault(c => c.Project == p && c.TaskId == t);
        if (row is null)
        {
            db.TaskClaims.Add(
                new TaskClaim
                {
                    Project = project,
                    TaskId = task,
                    AgentId = agent,
                    Title = title,
                    Since = Now,
                    Seq = seq,
                }
            );
        }
        else
        {
            row.AgentId = agent;
            row.Title = title;
            row.Since = Now;
            row.Seq = seq;
        }
        db.SaveChanges();
        scope.Commit();
    }

    /// <summary>Drops a task's claim; a missing one is fine.</summary>
    public void ClearTaskClaim(string project, string task)
    {
        using var scope = Enter();
        var db = scope.Db;
        var (p, t) = (project, task);
        db.TaskClaims.Where(c => c.Project == p && c.TaskId == t).ExecuteDelete();
    }

    /// <summary>The claim <paramref name="agent"/> made last in a project, or null.</summary>
    public TaskClaim? LatestTaskClaim(string project, string agent)
    {
        using var scope = Enter();
        var db = scope.Db;
        var (p, a) = (project, agent);
        return db
            .TaskClaims.Where(c => c.Project == p && c.AgentId == a)
            .OrderByDescending(c => c.Seq)
            .FirstOrDefault();
    }

    /// <summary>When an agent was last seen, in Unix seconds; null when there is no such agent.</summary>
    public long? AgentLastSeen(string agent)
    {
        using var scope = Enter();
        var db = scope.Db;
        var a = agent;
        return db.Agents.Where(x => x.Id == a).Select(x => (long?)x.LastSeen).FirstOrDefault();
    }

    /// <summary>The full id of the agent <paramref name="agent"/> names: the id itself, else
    /// the one agent whose id starts with it. Null when none or several match.</summary>
    public string? ResolveAgent(string agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (agent.Length == 0)
        {
            return null;
        }
        using var scope = Enter();
        var db = scope.Db;
        var a = agent;
        var exact = db.Agents.Where(x => x.Id == a).Select(x => x.Id).FirstOrDefault();
        if (exact is not null)
        {
            return exact;
        }
        var matches = db
            .Agents.Where(x => x.Id.StartsWith(a))
            .Select(x => x.Id)
            .OrderBy(x => x)
            .Take(2)
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }
}
