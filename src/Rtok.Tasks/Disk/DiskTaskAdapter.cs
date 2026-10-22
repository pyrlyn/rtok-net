using Pyrlyn.FsKit;

namespace Rtok.Tasks.Disk;

/// <summary>
/// Tasks as Markdown files in the checkout: active ones in <c>&lt;dir&gt;/</c>, done and
/// closed ones in <c>&lt;dir&gt;/done/</c>. Files are written temp-then-rename. Claims are
/// serialized by an OS lock on <c>&lt;dir&gt;/.claim.lock</c>, a file that is never deleted.
/// </summary>
/// <param name="dir">The task directory, absolute.</param>
/// <param name="clock">The clock for <c>created_at</c> and <c>updated_at</c>.</param>
public sealed class DiskTaskAdapter(string dir, TimeProvider clock) : ITaskAdapter
{
    /// <summary>How often a held claim lock is retried.</summary>
    public const int LockTries = 50;

    /// <summary>The wait between tries: about a second in all.</summary>
    public static readonly TimeSpan LockWait = TimeSpan.FromMilliseconds(20);

    private static readonly AtomicWriteOptions Options = new() { CreateParent = true };

    /// <summary>The task directory.</summary>
    public string Dir { get; } = dir;

    private string DoneDir => Path.Combine(Dir, "done");

    /// <inheritdoc/>
    public string Name => "disk";

    /// <inheritdoc/>
    public bool StoresMarkers => true;

    /// <inheritdoc/>
    public Task<PlanTask> CreateAsync(TaskId id, NewPlanTask task, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(task);
        var title = task.Title.Trim();
        if (title.Length == 0)
        {
            throw new TaskException("disk tasks: a task needs a title");
        }
        if (Locate(id) is not null)
        {
            throw new TaskTakenException($"disk tasks: {Dir}: task {id} already exists");
        }
        var now = Now();
        var created = new PlanTask
        {
            Id = id,
            Title = title,
            Description = task.Description.Trim(),
            Status = TaskState.Open,
            Parent = task.Parent,
            CreatedAt = now,
            UpdatedAt = now,
        };
        Place(created, null);
        return Task.FromResult(created);
    }

    /// <inheritdoc/>
    public Task<PlanTask?> FindAsync(TaskId id, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Task.FromResult(Locate(id)?.Task);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PlanTask>> ListAsync(
        TaskFilter filter,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(filter);
        var found = Scan(Dir);
        if (filter.WantsFinished)
        {
            found.AddRange(Scan(DoneDir));
        }
        return Task.FromResult(filter.Select(found.Select(f => f.Task)));
    }

    /// <inheritdoc/>
    public async Task<PlanTask> WriteStatusAsync(
        TaskId id,
        TaskState state,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(id);
        using var held = await LockClaimsAsync(ct).ConfigureAwait(false);
        var (path, task) = Locate(id) ?? throw Missing(id);
        task.Status = state;
        task.UpdatedAt = Now();
        Place(task, path);
        return task;
    }

    /// <inheritdoc/>
    public Task<PlanTask> SaveAsync(PlanTask task, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        var (path, _) = Locate(task.Id) ?? throw Missing(task.Id);
        var saved = task.Clone();
        saved.UpdatedAt = Now();
        saved.Normalize();
        Place(saved, path);
        return Task.FromResult(saved);
    }

    /// <inheritdoc/>
    public async Task<IDisposable> LockClaimsAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(Dir);
        var path = Path.Combine(Dir, ".claim.lock");
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                // FileShare.None is an exclusive OS lock (flock on Unix): released when the
                // handle closes or the process dies, so a leftover file never blocks anyone.
                return new FileStream(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.Write,
                    FileShare.None
                );
            }
            catch (IOException) when (attempt < LockTries)
            {
                await Task.Delay(LockWait, clock, ct).ConfigureAwait(false);
            }
            catch (IOException e)
            {
                throw new TaskException($"disk tasks: {path} is locked", e);
            }
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<StrayIssue>> UnlabelledAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<StrayIssue>>([]);

    private long Now() => clock.GetUtcNow().ToUnixTimeSeconds();

    private TaskException Missing(TaskId id) => new($"disk tasks: no task {id} in {Dir}");

    /// <summary>The plan copy first: after a crash between writing and deleting, it is the newer one.</summary>
    private (string Path, PlanTask Task)? Locate(TaskId id)
    {
        foreach (var folder in (string[])[Dir, DoneDir])
        {
            foreach (var found in Scan(folder))
            {
                if (found.Task.Id == id)
                {
                    return found;
                }
            }
        }
        return null;
    }

    private static List<(string Path, PlanTask Task)> Scan(string dir)
    {
        var found = new List<(string, PlanTask)>();
        if (!Directory.Exists(dir))
        {
            return found;
        }
        foreach (var path in Directory.EnumerateFiles(dir).Order(StringComparer.Ordinal))
        {
            if (TaskFile.FileId(Path.GetFileName(path)) is not { } named)
            {
                continue;
            }
            PlanTask task;
            try
            {
                task = TaskFile.Parse(File.ReadAllText(path));
            }
            catch (TaskException e)
            {
                throw new TaskException($"disk tasks: {path}: {e.Message}", e);
            }
            catch (IOException e)
            {
                throw new TaskException($"disk tasks: {path}: {e.Message}", e);
            }
            if (task.Id != named)
            {
                throw new TaskException(
                    $"disk tasks: {path}: file name says {named} but the front matter says {task.Id}"
                );
            }
            found.Add((path, task));
        }
        return found;
    }

    /// <summary>Writes the task where its state puts it, then removes the old file when the path changed.</summary>
    private void Place(PlanTask task, string? old)
    {
        var target = Path.Combine(
            task.Status.IsActive() ? Dir : DoneDir,
            TaskFile.FileName(task.Id, task.Title)
        );
        AtomicFile.Write(target, TaskFile.Render(task), Options);
        if (old is not null && !FsPath.SamePath(old, target))
        {
            File.Delete(old);
        }
    }
}
