using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CliWrap;

namespace Rtok.Doctor;

/// <summary>The real probes <c>rtok doctor</c> runs on a machine; tests pass fakes.</summary>
public static class DoctorProbes
{
    private const string Handshake =
        "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"rtok\",\"version\":\"0.1.0\"}}}\n"
        + "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}\n"
        + "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}\n";

    /// <summary>
    /// Starts the server with its args and env, writes initialize, initialized and tools/list,
    /// and reads JSON lines until one has <c>result.tools</c>; the child is killed then, or at the
    /// deadline. Null on any failure.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="limit">The one deadline.</param>
    /// <returns>Each tool's description.</returns>
    public static IReadOnlyList<string>? Mcp(McpCommand server, TimeSpan limit)
    {
        ArgumentNullException.ThrowIfNull(server);
        if (server.Command.Length == 0)
        {
            return null;
        }
        var program = Resolve(server.Command);
        var shim =
            OperatingSystem.IsWindows()
            && Path.GetExtension(program).ToUpperInvariant() is ".CMD" or ".BAT";
        var command = (
            shim
                ? Cli.Wrap(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe")
                    .WithArguments(["/d", "/s", "/c", program, .. server.Args])
                : Cli.Wrap(program).WithArguments(server.Args)
        )
            .WithEnvironmentVariables(
                server.Env.ToDictionary(
                    kv => kv.Key,
                    kv => (string?)kv.Value,
                    StringComparer.Ordinal
                )
            )
            .WithValidation(CommandResultValidation.None)
            .WithStandardInputPipe(PipeSource.FromString(Handshake));
        using var deadline = new CancellationTokenSource(limit);
        IReadOnlyList<string>? tools = null;
        command = command.WithStandardOutputPipe(
            PipeTarget.ToDelegate(line =>
            {
                if (tools is null && Tools(line) is { } found)
                {
                    tools = found;
                    deadline.Cancel();
                }
            })
        );
        try
        {
            command.ExecuteAsync(deadline.Token).Task.GetAwaiter().GetResult();
        }
        catch (Exception e)
            when (e
                    is OperationCanceledException
                        or System.ComponentModel.Win32Exception
                        or InvalidOperationException
                        or IOException
            )
        {
            // Killed once the tools came, or at the deadline; a program that cannot start has none.
        }
        return tools;
    }

    /// <summary>The descriptions in a line that answers tools/list; null for any other line.</summary>
    internal static List<string>? Tools(string line)
    {
        try
        {
            return DoctorHome.At(JsonNode.Parse(line), "result", "tools") is JsonArray tools
                ? [.. tools.Select(t => DoctorHome.Str(DoctorHome.At(t, "description")) ?? "")]
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A bare name found on <c>PATH</c>, with <c>PATHEXT</c> on Windows; else as written.</summary>
    private static string Resolve(string program)
    {
        if (program.AsSpan().ContainsAny('/', '\\'))
        {
            return program;
        }
        var exts = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries
            )
            : [""];
        foreach (
            var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries
            )
        )
        {
            foreach (var ext in exts)
            {
                var path = Path.Combine(dir, program + ext);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }
        return program;
    }

    /// <summary>
    /// A raw HTTP/1.1 <c>GET /health</c> to a loopback <paramref name="hostport"/>; the body, or
    /// null when nothing answers in time. Other hosts are never contacted.
    /// </summary>
    /// <param name="hostport">The host and port.</param>
    /// <param name="limit">How long it may take.</param>
    /// <returns>The body.</returns>
    public static string? Health(string hostport, TimeSpan limit)
    {
        ArgumentNullException.ThrowIfNull(hostport);
        var colon = hostport.LastIndexOf(':');
        if (
            colon < 0
            || !int.TryParse(
                hostport.AsSpan(colon + 1),
                System.Globalization.CultureInfo.InvariantCulture,
                out var port
            )
        )
        {
            return null;
        }
        var host = hostport[..colon].Trim('[', ']');
        if (!Exposure.Loopback(host))
        {
            return null;
        }
        using var deadline = new CancellationTokenSource(limit);
        try
        {
            using var client = new TcpClient();
            client.ConnectAsync(host, port, deadline.Token).AsTask().GetAwaiter().GetResult();
            using var stream = client.GetStream();
            var request = Encoding.ASCII.GetBytes(
                $"GET /health HTTP/1.1\r\nHost: {hostport}\r\nConnection: close\r\n\r\n"
            );
            stream.WriteAsync(request, deadline.Token).AsTask().GetAwaiter().GetResult();
            using var body = new MemoryStream();
            stream.CopyToAsync(body, deadline.Token).GetAwaiter().GetResult();
            var text = Encoding.UTF8.GetString(body.ToArray());
            var split = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            return split < 0 ? null : text[(split + 4)..];
        }
        catch (Exception e) when (e is OperationCanceledException or SocketException or IOException)
        {
            return null;
        }
    }
}
