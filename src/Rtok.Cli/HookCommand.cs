using ConsoleAppFramework;
using Rtok.Hook.Contracts;
using Rtok.Hooks;

namespace Rtok.Cli;

internal static class HookCommand
{
    /// <summary>Run one hook event from stdin, or serve hooks as this home's resident.</summary>
    /// <param name="hookEvent">The host's event name, such as PreToolUse.</param>
    /// <param name="host">The host id, when the host is not inferred from the payload.</param>
    /// <param name="serve">Run the resident hook server that rtok-hook talks to.</param>
    public static int Run(
        CancellationToken cancellationToken,
        [Argument] string hookEvent = "",
        string host = "",
        bool serve = false
    ) => serve ? Serve(cancellationToken) : Once(hookEvent, host);

    // In place, as the client's fallback runs it: always exit 0 with the no-op reply on a miss.
    private static int Once(string hookEvent, string host)
    {
        using var stdout = Console.OpenStandardOutput();
        try
        {
            byte[]? stdin;
            using (var input = Console.OpenStandardInput())
            {
                stdin = HookWire.ReadCapped(input);
            }
            if (stdin is null)
            {
                Console.Error.WriteLine(
                    $"rtok hook: stdin over {HookWire.MaxInput} bytes, skipped"
                );
                stdout.Write(HookWire.NoOp);
                return 0;
            }
            var request = HookRequest.Current(hookEvent, host, stdin);
            stdout.Write(HookHandlers.NoOp(request, replay: false).Span);
        }
#pragma warning disable CA1031 // Fail open: no failure may reach the host.
        catch (Exception)
#pragma warning restore CA1031
        {
            stdout.Write(HookWire.NoOp);
        }
        return 0;
    }

    private static int Serve(CancellationToken ct)
    {
        if (HookEndpoint.Current() is not { } endpoint)
        {
            Console.Error.WriteLine(
                "rtok hook --serve: the socket path in rtok's home is too long"
            );
            return 1;
        }
        Directory.CreateDirectory(endpoint.Home);
        // Off the host's project directory, which the resident would otherwise keep busy.
        Directory.SetCurrentDirectory(endpoint.Home);
        if (Environment.ProcessPath is { } exe)
        {
            HookClientLink.Ensure(Environment.GetEnvironmentVariable("PATH"), exe);
        }
        var exit = HookResident.Run(endpoint, HookHandlers.NoOp, ct);
        if (exit == ResidentExit.AlreadyRunning)
        {
            Console.Error.WriteLine(
                $"rtok hook --serve: a resident already serves {endpoint.Home}"
            );
        }
        return 0;
    }
}
