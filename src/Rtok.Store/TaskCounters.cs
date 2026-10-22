using Rtok.Store.Model;

namespace Rtok.Store;

/// <summary>A project's rows of <c>task_counters</c>, valid only inside
/// <see cref="RtokStore.WithTaskCounters{T}"/>, whose transaction they write in.</summary>
public sealed class TaskCounters
{
    private readonly RtokDbContext _db;
    private readonly string _project;

    internal TaskCounters(RtokDbContext db, string project)
    {
        _db = db;
        _project = project;
    }

    /// <summary>The last number handed out under <paramref name="parent"/> (empty for the top
    /// level), 0 when none.</summary>
    public long Last(string parent)
    {
        var db = _db;
        var (p, k) = (_project, parent);
        return db
            .TaskCounters.Where(c => c.Project == p && c.Parent == k)
            .Select(c => c.Last)
            .FirstOrDefault();
    }

    /// <summary>Sets the last number under <paramref name="parent"/>.</summary>
    public void SetLast(string parent, long last)
    {
        var db = _db;
        var (p, k) = (_project, parent);
        var row = db.TaskCounters.FirstOrDefault(c => c.Project == p && c.Parent == k);
        if (row is null)
        {
            db.TaskCounters.Add(
                new TaskCounter
                {
                    Project = _project,
                    Parent = parent,
                    Last = last,
                }
            );
        }
        else
        {
            row.Last = last;
        }
        db.SaveChanges();
    }
}
