// rtok-hook [<event>] [--host <id>]: the tiny hook client (N6, N8). It hands stdin to the
// resident (`rtok hook --serve`) and prints its reply. It always exits 0, and every miss prints
// the no-op reply (fail-open):
//   no resident, or one exiting for another version: queue the request and spawn the resident
//     detached, at most once per SpawnInterval;
//   a resident with another fingerprint, or no endpoint: hand stdin to a detached `rtok hook`;
//   no answer within 50 ms, or stdin over the cap: nothing more.
using Rtok.Hook;
using Rtok.Hook.Contracts;

using var stdout = Console.OpenStandardOutput();
try
{
    if (!TryParse(args, out var hookEvent, out var host))
    {
        stdout.Write(HookWire.NoOp);
        return 0;
    }
    byte[]? stdin;
    using (var input = Console.OpenStandardInput())
    {
        stdin = HookWire.ReadCapped(input);
    }
    if (stdin is null)
    {
        stdout.Write(HookWire.NoOp);
        Console.Error.WriteLine($"rtok-hook: stdin over {HookWire.MaxInput} bytes, skipped");
        return 0;
    }

    var request = HookRequest.Current(hookEvent, host, stdin);
    var endpoint = HookEndpoint.Current();
    if (endpoint is null)
    {
        stdout.Write(HookWire.NoOp);
        Fallback.RunOnce(request);
        return 0;
    }
    var body = HookWire.EncodeRequest(request);
    var outcome = HookClient.Exchange(endpoint, body, out var reply);
    stdout.Write(outcome == HookOutcome.Served ? reply : HookWire.NoOp);
    stdout.Flush();
    switch (outcome)
    {
        case HookOutcome.Unreachable or HookOutcome.RefusedVersion:
            Fallback.QueueAndSpawn(endpoint, body);
            break;
        case HookOutcome.RefusedFingerprint:
            Fallback.RunOnce(request);
            break;
    }
}
#pragma warning disable CA1031 // Fail open: no failure may reach the host.
catch (Exception)
#pragma warning restore CA1031
{
    try
    {
        stdout.Write(HookWire.NoOp);
    }
    catch (IOException) { }
}
return 0;

// `<event>`, `<event> --host <id>`, `--host <id>` or nothing; anything else is not ours.
static bool TryParse(string[] args, out string hookEvent, out string host)
{
    (hookEvent, host) = args switch
    {
        [] => ("", ""),
        ["--host", var h] => ("", h),
        [var e] when !e.StartsWith('-') => (e, ""),
        [var e, "--host", var h] when !e.StartsWith('-') => (e, h),
        _ => (null!, null!),
    };
    return hookEvent is not null;
}
