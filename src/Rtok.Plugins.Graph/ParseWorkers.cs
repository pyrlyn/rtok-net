using System.Runtime.ExceptionServices;
using System.Text.Unicode;

namespace Rtok.Plugins.Graph;

/// <summary>
/// Reads, hashes and parses jobs on one worker per core and hands each result to one writer,
/// the calling thread, in job order: the store sees exactly a sequential run's writes. At most
/// two results per worker wait for the writer, so a slow writer stalls the workers instead of
/// piling rows up; a writer that throws stops them at their next job.
/// </summary>
internal static class ParseWorkers
{
    public static Parsed Parse(ParseJob job)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(job.Path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new Parsed(ParseOutcome.Unreadable);
        }

        if (!Utf8.IsValid(bytes))
        {
            return new Parsed(ParseOutcome.NotText);
        }

        var sha = Extraction.Sha256(bytes);
        if (string.Equals(job.Known, sha, StringComparison.Ordinal))
        {
            return new Parsed(ParseOutcome.Same);
        }

        return Extraction.RowsOf(job.Path, bytes, job.Extensions) is { } rows
            ? new Parsed(ParseOutcome.Rows, sha, rows)
            : new Parsed(ParseOutcome.Unparsed);
    }

    public static void Each(IReadOnlyList<ParseJob> jobs, Action<ParseJob, Parsed> write) =>
        Each(jobs, Parse, write);

    internal static void Each(
        IReadOnlyList<ParseJob> jobs,
        Func<ParseJob, Parsed> parse,
        Action<ParseJob, Parsed> write
    )
    {
        var workers = Math.Min(Environment.ProcessorCount, jobs.Count);
        if (workers == 0)
        {
            return;
        }

        using var run = new Pipeline(jobs, parse, workers);
        for (var due = 0; due < jobs.Count; due++)
        {
            var slot = run.Take(due);
            slot.Error?.Throw();
            write(jobs[due], slot.Parsed);
        }
    }

    private readonly record struct Slot(bool Done, Parsed Parsed, ExceptionDispatchInfo? Error);

    // The workers and the slots they fill; disposing stops and joins the workers.
    private sealed class Pipeline : IDisposable
    {
        private readonly IReadOnlyList<ParseJob> _jobs;
        private readonly Func<ParseJob, Parsed> _parse;
        private readonly Slot[] _slots;
        private readonly SemaphoreSlim _window;
        private readonly object _gate = new(); // Monitor.Wait needs a monitor, not a Lock.
        private readonly Thread[] _threads;
        private int _next = -1;
        private bool _stop;

        public Pipeline(IReadOnlyList<ParseJob> jobs, Func<ParseJob, Parsed> parse, int workers)
        {
            (_jobs, _parse) = (jobs, parse);
            _slots = new Slot[jobs.Count];
            _window = new SemaphoreSlim(workers * 2);
            _threads = new Thread[workers];
            for (var w = 0; w < workers; w++)
            {
                _threads[w] = new Thread(Work) { IsBackground = true, Name = "rtok graph parse" };
                _threads[w].Start();
            }
        }

        public Slot Take(int due)
        {
            Slot slot;
            lock (_gate)
            {
                while (!_slots[due].Done)
                {
                    Monitor.Wait(_gate);
                }

                slot = _slots[due];
                _slots[due] = default;
            }

            _window.Release();
            return slot;
        }

        public void Dispose()
        {
            Volatile.Write(ref _stop, true);
            _window.Release(_threads.Length);
            foreach (var thread in _threads)
            {
                thread.Join();
            }

            _window.Dispose();
        }

        private void Work()
        {
            while (true)
            {
                _window.Wait();
                var i = Interlocked.Increment(ref _next);
                if (Volatile.Read(ref _stop) || i >= _jobs.Count)
                {
                    return;
                }

                var slot = Run(_jobs[i]);
                lock (_gate)
                {
                    _slots[i] = slot;
                    Monitor.PulseAll(_gate);
                }
            }
        }

#pragma warning disable CA1031 // A worker's failure is rethrown on the writer, never lost.
        private Slot Run(ParseJob job)
        {
            try
            {
                return new Slot(true, _parse(job), null);
            }
            catch (Exception e)
            {
                return new Slot(true, default, ExceptionDispatchInfo.Capture(e));
            }
        }
#pragma warning restore CA1031
    }
}
