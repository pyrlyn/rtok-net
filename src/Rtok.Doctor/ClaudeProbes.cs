using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pyrlyn.Config.Layered;
using Rtok.Core;
using Rtok.Plugin.Sdk;

namespace Rtok.Doctor;

/// <summary>
/// What Claude Code runs with: its MCP servers and their description cost, the base-URL chains
/// through proxies, tool search and the env knobs doctor prints.
/// </summary>
internal sealed class ClaudeProbes
{
    private const string AnthropicApi = "https://api.anthropic.com";

    private readonly DoctorHome _home;

    public ClaudeProbes(DoctorHome home)
    {
        _home = home;
        Settings = home.TryJson(home.PathOf(home.Config.Doctor.SettingsPath)) as JsonObject;
        ClaudeJson = home.TryJson(home.PathOf(home.Config.Doctor.ClaudeJson)) as JsonObject;
        McpJson = home.TryJson(home.PathOf(home.Config.Doctor.McpJson)) as JsonObject;
        Rates = new EstimatorRates(
            (float)home.Config.Estimator.Code,
            (float)home.Config.Estimator.Prose,
            (float)home.Config.Estimator.Json,
            (float)home.Config.Estimator.Cjk
        );
    }

    /// <summary>The user settings; null when missing or unparsable.</summary>
    public JsonObject? Settings { get; }

    /// <summary><c>~/.claude.json</c>.</summary>
    public JsonObject? ClaudeJson { get; }

    /// <summary>The project's <c>.mcp.json</c>.</summary>
    public JsonObject? McpJson { get; }

    public EstimatorRates Rates { get; }

    /// <summary>A variable of the settings' <c>env</c> table; null when unset or empty.</summary>
    public string? SettingsEnv(string name) =>
        DoctorHome.Str(DoctorHome.At(Settings, "env", name)) is { Length: > 0 } v ? v : null;

    /// <summary>
    /// The servers of <c>~/.claude.json</c>, then of <c>.mcp.json</c> (the first of a name wins),
    /// sorted, then rtok itself when no entry launches it; each asked for its tools.
    /// </summary>
    public List<McpServerRow> McpServers()
    {
        var servers = new SortedDictionary<string, McpCommand>(StringComparer.Ordinal);
        var own = false;
        foreach (var file in (JsonObject?[])[ClaudeJson, McpJson])
        {
            if (file?["mcpServers"] is not JsonObject table)
            {
                continue;
            }
            foreach (var (name, node) in table)
            {
                if (node is JsonObject entry && !servers.ContainsKey(name))
                {
                    servers[name] = Command(name, entry);
                    own |= McpFindings.IsOwn(entry, _home);
                }
            }
        }
        var rows = servers.Values.Select(Probe).ToList();
        if (!own && _home.Options.Rtok.Self is { } self)
        {
            rows.Add(
                Probe(
                    new McpCommand(
                        "rtok",
                        self,
                        ["mcp"],
                        new Dictionary<string, string>(StringComparer.Ordinal)
                    )
                )
            );
        }
        return rows;
    }

    private McpServerRow Probe(McpCommand server)
    {
        var cmd = string.Join(' ', [server.Command, .. server.Args]).Trim();
        if (server.Command.Length == 0)
        {
            return new McpServerRow(server.Name, cmd, 0, 0);
        }
        var limit = TimeSpan.FromMilliseconds(Math.Max(_home.Config.Doctor.McpTimeoutMs, 500));
        var tools = _home.Options.Mcp(server, limit) ?? [];
        var tokens = tools.Sum(d => TokenEstimator.Estimate(d, TextClass.Prose, Rates));
        return new McpServerRow(server.Name, cmd, tools.Count, tokens);
    }

    private static McpCommand Command(string name, JsonObject entry)
    {
        var args = entry["args"] is JsonArray a
            ? a.Select(DoctorHome.Str).OfType<string>().ToList()
            : [];
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        if (entry["env"] is JsonObject vars)
        {
            foreach (var (k, v) in vars)
            {
                if (DoctorHome.Str(v) is { } s)
                {
                    env[k] = s;
                }
            }
        }
        return new McpCommand(name, DoctorHome.Str(entry["command"]) ?? "", args, env);
    }

    /// <summary>
    /// <c>ANTHROPIC_BASE_URL</c>: the settings' env over the shell's; Anthropic's own endpoint
    /// counts as unset.
    /// </summary>
    public string? AnthropicBase()
    {
        var url = SettingsEnv("ANTHROPIC_BASE_URL") ?? _home.Var("ANTHROPIC_BASE_URL");
        return
            url is null
            || string.Equals(url.TrimEnd('/'), AnthropicApi, StringComparison.OrdinalIgnoreCase)
            ? null
            : url;
    }

    /// <summary>The OpenAI base: the shell, the settings, OpenCode's env, then Codex's rtok provider.</summary>
    public string? OpenaiBase()
    {
        if ((_home.Var("OPENAI_BASE_URL") ?? SettingsEnv("OPENAI_BASE_URL")) is { } url)
        {
            return url;
        }
        var opencode = _home.TryJson(_home.PathOf(_home.Config.Setup.Opencode.ConfigPath));
        if (
            DoctorHome.Str(DoctorHome.At(opencode, "env", "OPENAI_BASE_URL")) is
            { Length: > 0 } fromOpencode
        )
        {
            return fromOpencode;
        }
        var codex = _home.PathOf(_home.Config.Setup.Codex.ConfigPath);
        if (_home.TryRead(codex) is not { } text)
        {
            return null;
        }
        try
        {
            return
                DoctorHome.Str(
                    DoctorHome.At(
                        DoctorHome.ParseToml(text, codex),
                        "model_providers",
                        "rtok",
                        "base_url"
                    )
                )
                    is { Length: > 0 } b
                ? b
                : null;
        }
        catch (ConfigFileException)
        {
            return null;
        }
    }

    /// <summary>
    /// Follows a base URL through rtok proxies by their <c>/health</c> upstream: at most five
    /// hops, a loopback host shown by its port, stopping at a repeat.
    /// </summary>
    public string Chain(string? start)
    {
        var hops = new List<string>();
        var limit = TimeSpan.FromMilliseconds(Math.Max(_home.Config.Doctor.ProbeTimeoutMs, 300));
        for (var url = start; url is not null && hops.Count < 5; )
        {
            var hostport = HostPort(url);
            var label = Label(hostport);
            if (hops.Contains(label, StringComparer.Ordinal))
            {
                break;
            }
            hops.Add(label);
            url = Upstream(_home.Options.Health(hostport, limit));
        }
        return string.Join('→', hops);
    }

    /// <summary>The text after <c>://</c> up to the first <c>/</c>.</summary>
    internal static string HostPort(string url)
    {
        var at = url.IndexOf("://", StringComparison.Ordinal);
        var rest = at < 0 ? url : url[(at + 3)..];
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        return slash < 0 ? rest : rest[..slash];
    }

    /// <summary><c>127.0.0.1:8790</c> shows as <c>8790</c>; <c>localhost</c> stays.</summary>
    internal static string Label(string hostport) =>
        hostport.StartsWith("127.0.0.1:", StringComparison.Ordinal)
            ? hostport["127.0.0.1:".Length..]
            : hostport;

    private static string? Upstream(string? health)
    {
        if (health is null)
        {
            return null;
        }
        try
        {
            var body = JsonNode.Parse(health);
            return DoctorHome.Str(DoctorHome.At(body, "checks", "upstream", "url"))
                ?? DoctorHome.Str(DoctorHome.At(body, "config", "anthropic_api_url"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// <c>ENABLE_TOOL_SEARCH</c> from the settings' env, then the shell; without one, a custom
    /// base URL leaves it unknown.
    /// </summary>
    public ToolSearch ToolSearch()
    {
        var fromSettings = DoctorHome.At(Settings, "env", "ENABLE_TOOL_SEARCH") switch
        {
            JsonValue v when v.GetValueKind() == JsonValueKind.True => "true",
            JsonValue v when v.GetValueKind() == JsonValueKind.False => "false",
            var v => DoctorHome.Str(v),
        };
        foreach (
            var (value, where) in (ReadOnlySpan<(string?, string)>)
                [
                    (fromSettings, "settings.json env"),
                    (_home.Var("ENABLE_TOOL_SEARCH"), "environment"),
                ]
        )
        {
            if (
                value is not { Length: > 0 }
                || State(value.Trim().ToLowerInvariant()) is not { } state
            )
            {
                continue;
            }
            return new ToolSearch(state, $"ENABLE_TOOL_SEARCH={value} in {where}");
        }
        return AnthropicBase() is null
            ? new ToolSearch("enabled", null)
            : new ToolSearch("unknown", "heuristic: ANTHROPIC_BASE_URL set");
    }

    private static string? State(string value)
    {
        if (value is "false")
        {
            return "disabled";
        }
        if (value is "true" or "auto")
        {
            return "enabled";
        }
        return
            value.StartsWith("auto:", StringComparison.Ordinal)
            && byte.TryParse(
                value.AsSpan(5),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var n
            )
            && n <= 100
            ? "enabled"
            : null;
    }

    /// <summary>The advice to turn on <c>[proxy.tools_rewrite]</c>, or null.</summary>
    public string? ToolsRewrite(ToolSearch search, string chain, int descTokens)
    {
        var port = _home.Config.Proxy.Port.ToString(CultureInfo.InvariantCulture);
        var throughRtok = chain
            .Split('→')
            .Any(h =>
                string.Equals(h, port, StringComparison.Ordinal)
                || string.Equals(h, "localhost:" + port, StringComparison.Ordinal)
            );
        return
            search.State is not "enabled"
            && throughRtok
            && !_home.Config.Proxy.ToolsRewrite.Enabled
            && descTokens >= _home.Config.Doctor.ToolsRewriteMinDescTokens
            ? $"mcp descriptions ~{descTokens} tokens every turn; [proxy.tools_rewrite] enabled = true shortens them"
            : null;
    }

    /// <summary>The advice to load rtok's tools up front when Claude Code would defer them.</summary>
    public string? AlwaysLoad(ToolSearch search, IReadOnlyList<McpServerRow> servers)
    {
        var entry =
            DoctorHome.At(ClaudeJson, "mcpServers", "rtok")
            ?? DoctorHome.At(McpJson, "mcpServers", "rtok");
        var rtok = servers.FirstOrDefault(s => s.Name is "rtok");
        return
            entry is JsonObject o
            && !(o["alwaysLoad"] is JsonValue v && v.GetValueKind() == JsonValueKind.True)
            && rtok is { Tools: > 0 }
            && search.State is not "disabled"
            ? $"native Read denies cost a ToolSearch turn on Claude Code; \"alwaysLoad\": true on the rtok MCP entry loads its {rtok.Tools} tools (~{rtok.DescTokens} tokens) up front instead"
            : null;
    }
}
