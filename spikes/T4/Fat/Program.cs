// rtok-probe: the full binary's shape for T4.
//   --probe-exit-at-main      return before any app code: process start + static/module init
//     [--probe-t0 <ticks> <log>]  also append exec-to-Main microseconds (see hook-client)
//   hook <event> [--store]    config load, stdin parse, optional calls row, "{}"
//   serve --socket <path> [--store]  the resident: same work per frame, kept warm
//   serve-http, info          link Kestrel + MCP and Spectre.Console into the image
using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using ConsoleAppFramework;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Probe;
using Spectre.Console;
using Tomlyn;
using Tomlyn.Serialization;

if (args is ["--probe-exit-at-main"])
    return 0;
if (args is ["--probe-exit-at-main", "--probe-t0", var t0, var t0Log])
{
    var us = Stopwatch.GetElapsedTime(long.Parse(t0)).TotalMicroseconds;
    File.AppendAllText(t0Log, $"{us:F0}\n");
    return 0;
}

var app = ConsoleApp.Create();
app.Add<Commands>();
await app.RunAsync(args);
return Environment.ExitCode;

public sealed class Commands
{
    /// <summary>Run one hook event.</summary>
    /// <param name="name">Hook event name.</param>
    /// <param name="store">Also write one calls row.</param>
    [Command("hook")]
    public void Hook([Argument] string name, bool store = false)
    {
        var stdout = Console.OpenStandardOutput();
        try
        {
            var cfg = HookWork.LoadConfig();
            var body = HookWork.ReadStdin(cfg.Core.HookMaxInputBytes);
            StoreContext? db = store ? StoreContext.Open(HookWork.DbPath()) : null;
            stdout.Write(HookWork.Handle(body, name, db));
            db?.Dispose();
        }
        catch (Exception)
        {
            stdout.Write("{}"u8);
        }
    }

    /// <summary>Resident hook server on a Unix socket.</summary>
    /// <param name="socket">Socket path; its directory is created 0700.</param>
    /// <param name="store">Write one calls row per frame.</param>
    [Command("serve")]
    public void Serve(string socket, bool store = false)
    {
        var cfg = HookWork.LoadConfig();
        using var db = store ? StoreContext.Open(HookWork.DbPath()) : null;
        Directory.CreateDirectory(
            Path.GetDirectoryName(socket)!,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        );
        File.Delete(socket);
        using var listener = new Socket(
            AddressFamily.Unix,
            SocketType.Stream,
            ProtocolType.Unspecified
        );
        listener.Bind(new UnixDomainSocketEndPoint(socket));
        listener.Listen(64);
        var head = new byte[4];
        while (true)
        {
            using var c = listener.Accept();
            try
            {
                HookWork.ReadExactly(c, head);
                var body = new byte[BinaryPrimitives.ReadInt32LittleEndian(head)];
                HookWork.ReadExactly(c, body);
                var reply = HookWork.Handle(body, null, db);
                BinaryPrimitives.WriteInt32LittleEndian(head, reply.Length);
                c.Send(head);
                c.Send(reply);
            }
            catch (Exception) { }
            _ = cfg;
        }
    }

    /// <summary>MCP over Streamable HTTP (links Kestrel and the MCP SDK).</summary>
    [Command("serve-http")]
    public async Task ServeHttp()
    {
        var b = WebApplication.CreateSlimBuilder();
        b.Services.AddMcpServer().WithHttpTransport().WithTools<Tools>();
        var web = b.Build();
        web.MapMcp("/mcp");
        await web.RunAsync("http://127.0.0.1:8791");
    }

    /// <summary>Print a table (links Spectre.Console).</summary>
    [Command("info")]
    public void Info()
    {
        var t = new Table().AddColumn("key").AddColumn("value");
        t.AddRow("version", "probe");
        AnsiConsole.Write(t);
    }
}

[McpServerToolType]
public sealed class Tools
{
    [McpServerTool, Description("Echo")]
    public static string Echo(string message) => message;
}

static class HookWork
{
    public static string Home() =>
        Environment.GetEnvironmentVariable("RTOK_HOME")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".rtok");

    public static string DbPath() => Path.Combine(Home(), "rtok.db");

    // Layers: user config, then the project file in the cwd.
    public static Config LoadConfig()
    {
        var cfg = new Config();
        foreach (var path in new[] { Path.Combine(Home(), "config.toml"), ".rtok.toml" })
        {
            if (File.Exists(path))
                cfg =
                    TomlSerializer.Deserialize(File.ReadAllText(path), TomlCtx.Default.Config)
                    ?? cfg;
        }
        return cfg;
    }

    public static byte[] ReadStdin(int cap)
    {
        using var stdin = Console.OpenStandardInput();
        var buf = new byte[64 << 10];
        var len = 0;
        while (true)
        {
            if (len == buf.Length)
            {
                if (buf.Length >= cap)
                    return [];
                Array.Resize(ref buf, Math.Min(buf.Length * 2, cap));
            }
            var n = stdin.Read(buf, len, buf.Length - len);
            if (n == 0)
                return buf[..len];
            len += n;
        }
    }

    public static byte[] Handle(byte[] body, string? ev, StoreContext? db)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        ev ??= root.TryGetProperty("hook_event_name", out var e) ? e.GetString() ?? "" : "";
        var tool = root.TryGetProperty("tool_name", out var t) ? t.GetString() ?? "" : "";
        db?.Record("hook", ev, tool, body.Length);
        return "{}"u8.ToArray();
    }

    public static void ReadExactly(Socket s, byte[] buf)
    {
        var off = 0;
        while (off < buf.Length)
        {
            var n = s.Receive(buf, off, buf.Length - off, SocketFlags.None);
            if (n == 0)
                throw new IOException("closed");
            off += n;
        }
    }
}

public sealed class Config
{
    [TomlPropertyName("core")]
    public CoreConfig Core { get; set; } = new();

    [TomlPropertyName("hook")]
    public HookConfig Hook { get; set; } = new();

    [TomlPropertyName("plugins")]
    public Dictionary<string, PluginConfig> Plugins { get; set; } = [];
}

public sealed class CoreConfig
{
    [TomlPropertyName("hook_max_input_bytes")]
    public int HookMaxInputBytes { get; set; } = 8 << 20;

    [TomlPropertyName("log_level")]
    public string LogLevel { get; set; } = "info";
}

public sealed class HookConfig
{
    [TomlPropertyName("max_ms")]
    public int MaxMs { get; set; } = 10;
}

public sealed class PluginConfig
{
    [TomlPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [TomlPropertyName("budget")]
    public int Budget { get; set; }
}

[TomlSerializable(typeof(Config))]
internal sealed partial class TomlCtx : TomlSerializerContext;
