namespace Rtok.Process;

/// <summary>
/// What the two pipe readers of one run share: the indicator, the parser, and a byte budget
/// for stdout and stderr together.
/// </summary>
internal sealed class Pump(IIndicator indicator, IProgressParser? parser, long cap, bool forward)
{
    private static readonly Lazy<Stream> OurStdout = new(System.Console.OpenStandardOutput);
    private static readonly Lazy<Stream> OurStderr = new(System.Console.OpenStandardError);

    private readonly Lock parserLock = new();
    private long left = cap;
    private long dropped;

    /// <summary>Bytes read past the cap and not kept.</summary>
    public long Dropped => Interlocked.Read(ref dropped);

    /// <summary>
    /// Reads <paramref name="source"/> into <paramref name="sink"/> as far as the budget allows
    /// (or forwards it live), and feeds its lines to the parser, until the end of the stream or,
    /// once <paramref name="deadline"/> has passed, until a read would wait.
    /// </summary>
    public async Task DrainAsync(Stream source, ChildOutput which, MemoryStream sink, Task deadline)
    {
        var buffer = new byte[8192];
        var lines = new LineSplitter();
        while (true)
        {
            var read = source.ReadAsync(buffer).AsTask();
            if (await Task.WhenAny(read, deadline).ConfigureAwait(false) != read)
            {
                // A descendant holds the pipe open; leave the read behind.
                _ = read.ContinueWith(
                    static t => t.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default
                );
                break;
            }
            int n;
            try
            {
                n = await read.ConfigureAwait(false);
            }
            catch (IOException)
            {
                break;
            }
            if (n == 0)
            {
                break;
            }
            if (forward)
            {
                var target = which is ChildOutput.Stdout ? OurStdout.Value : OurStderr.Value;
                indicator.Suspend(() =>
                {
                    target.Write(buffer, 0, n);
                    target.Flush();
                });
            }
            else
            {
                var keep = Take(n);
                lock (sink)
                {
                    sink.Write(buffer, 0, keep);
                }
            }
            lines.Push(buffer.AsSpan(0, n), line => Feed(which, line));
        }
        lines.Finish(line => Feed(which, line));
    }

    /// <summary>Takes up to <paramref name="n"/> bytes from the budget; the rest of them are counted as dropped.</summary>
    internal int Take(int n)
    {
        long before;
        long got;
        do
        {
            before = Interlocked.Read(ref left);
            got = Math.Min(before, n);
        } while (Interlocked.CompareExchange(ref left, before - got, before) != before);
        Interlocked.Add(ref dropped, n - got);
        return (int)got;
    }

    private void Feed(ChildOutput which, string line)
    {
        if (parser is null)
        {
            return;
        }
        ProgressUpdate? update;
        lock (parserLock)
        {
            update = parser.Feed(which, line);
        }
        if (update is not null)
        {
            indicator.Report(update);
        }
    }
}
