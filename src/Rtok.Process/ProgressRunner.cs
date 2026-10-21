using System.Diagnostics;
using CliWrap;
using CliWrap.Builders;

namespace Rtok.Process;

/// <summary>
/// The one way rtok runs an external program: a spinner or a bar on a terminal while the child
/// works, and nothing anywhere else. Built on CliWrap; <c>Process.Start</c> is banned everywhere
/// but this project.
/// </summary>
public sealed class ProgressRunner
{
    /// <summary>
    /// How long a run keeps reading once the child has exited. A descendant it left running (a
    /// file-system monitor, a backgrounded server) can hold a pipe's write end for good, so
    /// waiting for the end of the stream would hang; what the pipe already holds is still read.
    /// </summary>
    internal static readonly TimeSpan DrainAfterExit = TimeSpan.FromMilliseconds(200);

    private readonly string label;
    private readonly string program;
    private readonly List<(string Value, bool Raw)> arguments;
    private readonly Dictionary<string, string?> environment = new(StringComparer.Ordinal);
    private string? workingDirectory;
    private Stream? standardInput;
    private TimeSpan? timeout;
    private OutputMode output;
    private bool shown = true;
    private IProgressParser? parser;
    private bool shim;

    /// <summary>A run with a spinner.</summary>
    /// <param name="label">The message beside the spinner: the action and its target, present tense, ending in <c>…</c>.</param>
    /// <param name="program">The program: a name looked up on PATH, or a path.</param>
    /// <param name="arguments">Its arguments, each passed as one word.</param>
    public ProgressRunner(string label, string program, IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentException.ThrowIfNullOrEmpty(program);
        ArgumentNullException.ThrowIfNull(arguments);
        this.label = label;
        this.program = program;
        this.arguments = [.. arguments.Select(a => (a, false))];
    }

    /// <summary>A run with no indicator: a quick read nobody waits for.</summary>
    /// <param name="program">The program.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <returns>The runner.</returns>
    public static ProgressRunner Quiet(string program, IEnumerable<string> arguments) =>
        new ProgressRunner("", program, arguments).NoProgress();

    /// <summary>A bar over <paramref name="total"/> items that rtok works through itself.</summary>
    /// <param name="label">The label.</param>
    /// <param name="total">The number of items.</param>
    /// <param name="unit">What the items are.</param>
    /// <returns>The batch.</returns>
    public static Batch Batch(string label, long total, ProgressUnit unit) =>
        new(ProgressDisplay.Bar(label, total, unit), total, unit);

    /// <summary>A spinner while rtok works in-process (a migration, a report), cleared before it returns.</summary>
    /// <typeparam name="T">The result.</typeparam>
    /// <param name="label">The label.</param>
    /// <param name="work">The work.</param>
    /// <returns>What the work returned.</returns>
    public static T Wait<T>(string label, Func<T> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        var indicator = ProgressDisplay.Spinner(label);
        try
        {
            return work();
        }
        finally
        {
            indicator.Finish();
        }
    }

    /// <summary>
    /// True when <paramref name="command"/> is a Windows batch file or a <c>PATHEXT</c> shim that
    /// <c>CreateProcess</c> cannot run: a <c>.cmd</c> or <c>.bat</c> path, or a bare name
    /// (<c>npx</c>) that <c>PATHEXT</c> may resolve to one.
    /// </summary>
    /// <param name="command">The program.</param>
    /// <returns>Whether it needs <c>cmd.exe</c>.</returns>
    public static bool NeedsCmdHost(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (
            command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || command.EndsWith(".com", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }
        return command.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            || command.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
            || command.IndexOfAny(['/', '\\', ':']) < 0;
    }

    /// <summary>The child's working directory.</summary>
    /// <param name="directory">The directory.</param>
    /// <returns>This runner.</returns>
    public ProgressRunner WorkingDirectory(string directory)
    {
        workingDirectory = directory;
        return this;
    }

    /// <summary>Sets a variable in the child's environment; null removes it.</summary>
    /// <param name="name">The name.</param>
    /// <param name="value">The value, or null.</param>
    /// <returns>This runner.</returns>
    public ProgressRunner Environment(string name, string? value)
    {
        environment[name] = value;
        return this;
    }

    /// <summary>Feeds the child this stream. Without it a piped or detached child gets a closed stdin; an inherited one keeps ours.</summary>
    /// <param name="input">The input.</param>
    /// <returns>This runner.</returns>
    public ProgressRunner StandardInput(Stream input)
    {
        standardInput = input;
        return this;
    }

    /// <summary>
    /// One argument put on the Windows command line as it is, for <c>cmd.exe /C</c> bodies that
    /// the usual quoting (<c>\"</c>) would break, since <c>cmd.exe</c> does not parse it.
    /// </summary>
    /// <param name="argument">The argument.</param>
    /// <returns>This runner.</returns>
    public ProgressRunner RawArgument(string argument)
    {
        arguments.Add((argument, true));
        return this;
    }

    /// <summary>
    /// Runs Windows CLIs installed through npm (<c>npx</c>, <c>claude</c>), which ship as
    /// <c>.cmd</c> shims, through <c>cmd.exe /D /C</c> as the shell does. A no-op elsewhere and
    /// for an <c>.exe</c> or a path.
    /// </summary>
    /// <returns>This runner.</returns>
    public ProgressRunner WindowsShim()
    {
        shim = true;
        return this;
    }

    /// <summary>Kills the child and what it started after <paramref name="limit"/>, and fails with <see cref="TimeoutException"/>.</summary>
    /// <param name="limit">The limit.</param>
    /// <returns>This runner.</returns>
    public ProgressRunner Timeout(TimeSpan limit)
    {
        timeout = limit;
        return this;
    }

    /// <summary>No indicator.</summary>
    /// <returns>This runner.</returns>
    public ProgressRunner NoProgress()
    {
        shown = false;
        return this;
    }

    /// <summary>A spinner that turns into a bar when <paramref name="lineParser"/> reports a total.</summary>
    /// <param name="lineParser">Reads the child's lines.</param>
    /// <returns>This runner.</returns>
    public ProgressRunner Progress(IProgressParser lineParser)
    {
        parser = lineParser;
        shown = true;
        return this;
    }

    /// <summary>Where the child's output goes; <see cref="OutputMode.Capture"/> by default.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>This runner.</returns>
    public ProgressRunner Output(OutputMode mode)
    {
        output = mode;
        return this;
    }

    /// <summary>Runs to the end. The indicator is cleared first, so the caller's next line lands on a clean one.</summary>
    /// <param name="cancellationToken">Kills the child.</param>
    /// <returns>The result.</returns>
    /// <exception cref="System.ComponentModel.Win32Exception">The program cannot start.</exception>
    /// <exception cref="TimeoutException">The child ran past its timeout.</exception>
    public Task<ProcessResult> RunAsync(CancellationToken cancellationToken = default) =>
        RunCappedAsync(long.MaxValue, cancellationToken);

    /// <summary>
    /// <see cref="RunAsync"/> that keeps at most <paramref name="cap"/> bytes of stdout and stderr
    /// together. Bytes past it are still read, so the child never blocks on a full pipe, but are
    /// only counted.
    /// </summary>
    /// <param name="cap">The most bytes kept.</param>
    /// <param name="cancellationToken">Kills the child.</param>
    /// <returns>The result.</returns>
    public Task<ProcessResult> RunCappedAsync(
        long cap,
        CancellationToken cancellationToken = default
    ) =>
        output switch
        {
            OutputMode.Capture or OutputMode.Stream => RunPipedAsync(cap, cancellationToken),
            OutputMode.Inherit => RunInheritedAsync(cancellationToken),
            _ => throw DoesNotFit("a detached child"),
        };

    /// <summary>Starts a child the caller owns: <see cref="OutputMode.Capture"/> pipes stdout and stderr, <see cref="OutputMode.Detached"/> gives it nothing, <see cref="OutputMode.Inherit"/> shares ours.</summary>
    /// <returns>The started process.</returns>
    /// <exception cref="System.ComponentModel.Win32Exception">The program cannot start.</exception>
    public System.Diagnostics.Process Spawn()
    {
        if (output is OutputMode.Stream)
        {
            throw DoesNotFit("a streamed child");
        }
        var (file, args) = Command();
        if (output is OutputMode.Detached && !OperatingSystem.IsWindows())
        {
            // exec keeps the pid, so the caller still owns the child; the shell only redirects.
            args =
            [
                ("-c", false),
                ("exec \"$0\" \"$@\" </dev/null >/dev/null 2>&1", false),
                (file, false),
                .. args,
            ];
            file = "/bin/sh";
        }
        var start = StartInfo(file, args);
        var feed = output is not OutputMode.Inherit || standardInput is not null;
        start.RedirectStandardInput = feed;
        start.RedirectStandardOutput =
            output is OutputMode.Capture
            || OperatingSystem.IsWindows() && output is OutputMode.Detached;
        start.RedirectStandardError = start.RedirectStandardOutput;
        start.CreateNoWindow = output is OutputMode.Detached;
        var child = System.Diagnostics.Process.Start(start)!;
        if (feed)
        {
            _ = FeedAsync(child.StandardInput.BaseStream, standardInput);
        }
        if (output is OutputMode.Detached && start.RedirectStandardOutput)
        {
            child.BeginOutputReadLine();
            child.BeginErrorReadLine();
        }
        return child;
    }

    private static InvalidOperationException DoesNotFit(string what) =>
        new($"{what} does not fit this call: see ProgressRunner.RunAsync and Spawn");

    private static async Task FeedAsync(Stream stdin, Stream? input)
    {
        await using (stdin.ConfigureAwait(false))
        {
            if (input is not null)
            {
                await input.CopyToAsync(stdin).ConfigureAwait(false);
            }
        }
    }

    private (string File, IReadOnlyList<(string Value, bool Raw)> Args) Command() =>
        shim && OperatingSystem.IsWindows() && NeedsCmdHost(program)
            ? ("cmd.exe", [("/D", false), ("/C", false), (program, false), .. arguments])
            : (program, arguments);

    private ProcessStartInfo StartInfo(string file, IReadOnlyList<(string Value, bool Raw)> args)
    {
        var start = new ProcessStartInfo(file) { UseShellExecute = false, Arguments = Join(args) };
        if (workingDirectory is not null)
        {
            start.WorkingDirectory = workingDirectory;
        }
        foreach (var (name, value) in environment)
        {
            if (value is null)
            {
                start.Environment.Remove(name);
            }
            else
            {
                start.Environment[name] = value;
            }
        }
        return start;
    }

    private static string Join(IReadOnlyList<(string Value, bool Raw)> args)
    {
        var builder = new ArgumentsBuilder();
        foreach (var (value, raw) in args)
        {
            builder.Add(value, escape: !raw);
        }
        return builder.Build();
    }

    private async Task<ProcessResult> RunPipedAsync(long cap, CancellationToken cancellationToken)
    {
        var (file, args) = Command();
        var indicator = shown ? ProgressDisplay.Spinner(label) : HiddenIndicator.Instance;
        try
        {
            var pump = new Pump(indicator, parser, cap, output is OutputMode.Stream);
            var exited = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            var deadline = exited
                .Task.ContinueWith(
                    _ => Task.Delay(DrainAfterExit),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default
                )
                .Unwrap();
            using var stdout = new MemoryStream();
            using var stderr = new MemoryStream();
            var command = Wrap(
                file,
                args,
                PipeTarget.Create(
                    (s, _) => pump.DrainAsync(s, ChildOutput.Stdout, stdout, deadline)
                ),
                PipeTarget.Create(
                    (s, _) => pump.DrainAsync(s, ChildOutput.Stderr, stderr, deadline)
                )
            );
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeout is { } after)
            {
                limit.CancelAfter(after);
            }
            var task = command.ExecuteAsync(limit.Token);
            _ = WatchExitAsync(task.ProcessId, exited);
            try
            {
                var result = await task.ConfigureAwait(false);
                return new ProcessResult(
                    result.ExitCode,
                    stdout.ToArray(),
                    stderr.ToArray(),
                    pump.Dropped
                );
            }
            catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("the command timed out", e);
            }
        }
        finally
        {
            indicator.Finish();
        }
    }

    private CliWrap.Command Wrap(
        string file,
        IReadOnlyList<(string Value, bool Raw)> args,
        PipeTarget stdout,
        PipeTarget stderr
    )
    {
        var command = Cli.Wrap(file)
            .WithArguments(Join(args))
            .WithEnvironmentVariables(e =>
            {
                foreach (var (name, value) in environment)
                {
                    e.Set(name, value);
                }
            })
            .WithStandardInputPipe(
                standardInput is null ? PipeSource.Null : PipeSource.FromStream(standardInput)
            )
            .WithStandardOutputPipe(stdout)
            .WithStandardErrorPipe(stderr)
            .WithValidation(CommandResultValidation.None);
        return workingDirectory is null ? command : command.WithWorkingDirectory(workingDirectory);
    }

    private async Task<ProcessResult> RunInheritedAsync(CancellationToken cancellationToken)
    {
        using var child = Spawn();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout is { } after)
        {
            limit.CancelAfter(after);
        }
        try
        {
            await child.WaitForExitAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException e)
        {
            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            throw new TimeoutException("the command timed out", e);
        }
        return new ProcessResult(child.ExitCode, [], [], 0);
    }

    /// <summary>Completes <paramref name="exited"/> when the child exits; a child already gone counts as exited.</summary>
    private static async Task WatchExitAsync(int pid, TaskCompletionSource exited)
    {
        try
        {
            using var child = System.Diagnostics.Process.GetProcessById(pid);
            await child.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
        finally
        {
            exited.TrySetResult();
        }
    }
}
