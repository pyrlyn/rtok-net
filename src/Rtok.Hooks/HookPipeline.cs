using System.Globalization;
using Rtok.Config;
using Rtok.Hook.Contracts;
using Rtok.Hooks.Hosts;
using Rtok.Store;

namespace Rtok.Hooks;

/// <summary>
/// One hook event from the host's bytes to the reply it reads (rtok <c>hooks::run</c>): the
/// stdin cap, the host adapter, the store under the hook's lock policy, the plugin dispatch
/// (<see cref="Answer"/>) and the host writer. It fails open: anything that goes wrong answers
/// <c>{}</c>. Every row an event writes lands in one transaction, so a locked store costs one
/// 5 ms attempt and writes nothing. A resident keeps one pipeline, and with it the open store
/// and the replies of the tool calls it has already answered.
/// </summary>
public sealed partial class HookPipeline(HookPipelineOptions options) : IDisposable
{
    private const int Remembered = 256;

    // rtok LOCK_WAIT: one attempt, at most 5 ms, for the lock and for a migration.
    private static readonly TimeSpan LockWait = TimeSpan.FromMilliseconds(5);

    private readonly Dictionary<string, byte[]> _replies = new(StringComparer.Ordinal);
    private readonly Queue<string> _order = new();
    private RtokStore? _store;
    private StoreOptions? _storeOptions;

    /// <summary>Runs one event in place (<c>rtok hook &lt;event&gt;</c>), reading stdin up to
    /// <c>core.hook_max_input_bytes</c>.</summary>
    /// <param name="hookEvent">The event argument.</param>
    /// <param name="host"><c>--host</c>, empty when not given.</param>
    /// <param name="stdin">The host's payload.</param>
    /// <param name="cwd">The working directory.</param>
    /// <returns>The reply; <c>{}</c> on any failure.</returns>
    public byte[] RunInPlace(string hookEvent, string host, Stream stdin, string cwd)
    {
        try
        {
            var config = Load(cwd, host);
            var cap = (int)Math.Min(config.Config.Core.HookMaxInputBytes, Array.MaxLength - 1L);
            var deferred = options.Environment(HookDeferral.Variable);
            var body =
                HookDeferral.Take(config.Locations.Home, deferred)
                ?? HookWire.ReadCapped(stdin, cap);
            return body is null
                ? OverCap(hookEvent, cap, cap + 1L)
                : Run(config, new Call(hookEvent, body, options.Environment, deferred is not null));
        }
#pragma warning disable CA1031 // Fail open: no failure may reach the host.
        catch (Exception e)
#pragma warning restore CA1031
        {
            return Failed(hookEvent, e);
        }
    }

    /// <summary>The resident's <see cref="HookHandler"/>.</summary>
    /// <param name="request">The client's call.</param>
    /// <param name="replay">True for a call drained from the backlog: the host already has
    /// its answer.</param>
    public ReadOnlyMemory<byte> Handle(HookRequest request, bool replay)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var config = Load(request.Cwd, request.Host);
            var cap = config.Config.Core.HookMaxInputBytes;
            if (request.Stdin.Length > cap)
            {
                return OverCap(request.Event, cap, request.Stdin.Length);
            }
            // Only the host's own variables travel with a call, and the session variable is
            // not one of them: the resident serves every session.
            var hostEnv = request.HostEnv.ToDictionary(StringComparer.Ordinal);
            string? env(string name) => hostEnv.TryGetValue(name, out var value) ? value : null;
            return Run(
                config,
                new Call(request.Event, request.Stdin.ToArray(), env, Deferred: false, replay)
            );
        }
#pragma warning disable CA1031 // Fail open: no failure may reach the host.
        catch (Exception e)
#pragma warning restore CA1031
        {
            return Failed(request.Event, e);
        }
    }

    // The store the last event ran on, for the tests that count its commits.
    internal RtokStore? OpenStore => _store;

    public void Dispose()
    {
        _store?.Dispose();
        _store = null;
    }

    private byte[] Run(LoadedConfig config, Call call)
    {
        var core = config.Config.Core;
        var input = core.Enabled ? HookInput.Parse(call.Stdin) : null;
        if (input is null)
        {
            return NoOp();
        }
        var host = config.Config.Hook.Host;
        var adapted = HostAdapters.Adapt(host, call.Event, input, call.Env);
        var once = input.ToolUseId is { Length: > 0 } id ? $"{input.HookEventName}:{id}" : null;
        if (once is not null && _replies.TryGetValue(once, out var answered))
        {
            return answered;
        }
        // The ledger keeps what the plugins read: an adapted host's payload in Claude's shape.
        var stored = adapted ? input.ToJson() : call.Stdin;
        var hookEvent = call.Event.Length > 0 ? call.Event : input.HookEventName;
        if (TryBegin(config, call, input, stored, hookEvent) is not var (store, batch))
        {
            return NoOp();
        }
        using (batch)
        {
            var start = options.Time.GetTimestamp();
            var hostId = store.HostId(host) ?? store.HostId("other");
            var runtime = Runtime(store, config, input, call, once, hostId);
            var callId = runtime.RecordCall("hook", "hook", hookEvent);
            var reply = Answer(config, input, runtime, store, hostId);
            var bytes = HostWriters.Write(host, reply, input.Prompt);
            var ms = options.Time.GetElapsedTime(start).TotalMilliseconds;
            if (!call.Replay)
            {
                NoteSlow(config.Config.Hook.MaxMs, hookEvent, ms, runtime);
            }
            store.SetCallMs(callId, ms);
            store.InsertHookCallIo(callId, stored, bytes);
            batch.Commit();
            Remember(once, bytes);
            return bytes;
        }
    }

    // The store and the event's one write transaction, or null when the hook must answer {}.
    private (RtokStore Store, StoreBatch Batch)? TryBegin(
        LoadedConfig config,
        Call call,
        HookInput input,
        byte[] stored,
        string hookEvent
    )
    {
        RtokStore store;
        try
        {
            store = Store(config.Config.Core, steady: call.Deferred);
            return (store, store.BeginBatch());
        }
        catch (Exception e) when (RtokStore.IsLocked(e))
        {
            options.Errors.WriteLine($"rtok: hook {hookEvent} skipped: store locked: {e.Message}");
            if (input.HookEventName is "SessionEnd" && !call.Deferred && !call.Replay)
            {
                HookDeferral.Defer(config.Locations.Home, options.Executable, stored);
            }
            return null;
        }
    }

    private RtokStore Store(CoreSection core, bool steady)
    {
        var wanted = new StoreOptions(core.DbPath)
        {
            ArchiveDir = core.ArchiveDir,
            CallIoInlineBytes = (int)Math.Min(core.CallIoInlineBytes, int.MaxValue),
            StoreRaw = core.StoreRaw,
        };
        if (!steady)
        {
            wanted = wanted with
            {
                FailFast = true,
                Busy = LockWait,
                MaintenanceBusy = LockWait,
                ConnectAttempts = 1,
            };
        }
        if (_store is not null && _storeOptions == wanted)
        {
            return _store;
        }
        Dispose();
        _store = RtokStore.Open(wanted);
        _storeOptions = wanted;
        return _store;
    }

    private LoadedConfig Load(string cwd, string host) =>
        ConfigLoader.Load(
            new ConfigLoadOptions
            {
                ConfigFile = options.ConfigFile,
                WorkingDirectory = Directory.Exists(cwd) ? cwd : null,
                Environment = options.ConfigEnvironment,
                Flags = host.Length == 0 ? [] : [new ConfigFlag("hook.host", host, "--host")],
            }
        );

    // rtok slow_note: over [hook] max_ms, one stderr line and one warn row; 0 turns it off.
    private void NoteSlow(ulong maxMs, string hookEvent, double ms, StoreRuntime runtime)
    {
        if (maxMs == 0 || ms <= maxMs)
        {
            return;
        }
        var note = string.Create(
            CultureInfo.InvariantCulture,
            $"hook {hookEvent} slow: {ms:F1} ms over max_ms {maxMs} ms"
        );
        options.Errors.WriteLine($"rtok: {note}");
        runtime.Log("warn", "hook", hookEvent, note);
    }

    private byte[] OverCap(string hookEvent, long cap, long got)
    {
        options.Errors.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"rtok: hook {hookEvent} stdin over core.hook_max_input_bytes ({cap} bytes, got {got}); failing open"
            )
        );
        return NoOp();
    }

    private byte[] Failed(string hookEvent, Exception e)
    {
        options.Errors.WriteLine($"rtok: hook {hookEvent} failed: {e.Message}; failing open");
        return NoOp();
    }

    private void Remember(string? once, byte[] reply)
    {
        if (once is null)
        {
            return;
        }
        if (_order.Count >= Remembered)
        {
            _replies.Remove(_order.Dequeue());
        }
        _replies[once] = reply;
        _order.Enqueue(once);
    }

    private static byte[] NoOp() => HookWire.NoOp.ToArray();

    private sealed record Call(
        string Event,
        byte[] Stdin,
        Func<string, string?> Env,
        bool Deferred,
        bool Replay = false
    );
}
