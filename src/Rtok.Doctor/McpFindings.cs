using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rtok.Doctor;

/// <summary>
/// Duplicate MCP servers per host surface (rtok D33): rtok's own entries apart, then one name
/// in several scopes, then one launch under several names, then one package at several
/// versions, then rtok configured more than once.
/// </summary>
internal static class McpFindings
{
    private static readonly HashSet<string> Runners = new(StringComparer.Ordinal)
    {
        "npx",
        "bunx",
        "pnpx",
        "uvx",
    };

    public static List<Problem> Find(
        DoctorHome home,
        IReadOnlyList<McpEntry> entries,
        GroupCounter groups
    )
    {
        var running = entries
            .Select(e => (Entry: e, Launch: LaunchOf(e.Node, home)))
            .Where(x => x.Launch is not null)
            .Select(x => (x.Entry, Launch: x.Launch!.Value))
            .ToList();
        var own = running.Where(x => IsOwn(x.Entry.Node, home)).ToList();
        var others = running.Where(x => !IsOwn(x.Entry.Node, home)).ToList();
        var problems = new List<Problem>();
        var live = new List<(McpEntry Entry, Launch Launch)>();
        foreach (var named in others.GroupBy(x => (x.Entry.Set, x.Entry.Name)))
        {
            var copies = named.ToList();
            var used = copies.OrderByDescending(x => x.Entry.Wins).First();
            live.Add(used);
            if (copies.Count < 2)
            {
                continue;
            }
            var id = groups.Next();
            foreach (var (entry, launch) in copies)
            {
                var keep = ReferenceEquals(entry, used.Entry);
                var detail = keep
                    ? $"`{entry.Name}` is defined {copies.Count} times; the host uses this one"
                    : $"`{entry.Name}` is defined {copies.Count} times; unused, overridden by another scope";
                problems.Add(
                    Of(
                        entry,
                        launch,
                        ProblemKinds.DuplicateMcp,
                        detail,
                        entry.Scope != Scope.Plugin,
                        id,
                        keep
                    )
                );
            }
        }
        live =
        [
            .. live.OrderBy(x => x.Entry.Set, StringComparer.Ordinal)
                .ThenBy(x => x.Entry.Name, StringComparer.Ordinal),
        ];
        problems.AddRange(Launches(live, groups));
        problems.AddRange(Versions(live, groups));
        foreach (
            var set in own.GroupBy(x => x.Entry.Set, StringComparer.Ordinal)
                .Where(g => g.Skip(1).Any())
        )
        {
            problems.AddRange(Own([.. set], groups));
        }
        return problems;
    }

    /// <summary>One launch under several names: the copy in the narrowest shared scope stays.</summary>
    private static IEnumerable<Problem> Launches(
        List<(McpEntry Entry, Launch Launch)> live,
        GroupCounter groups
    )
    {
        foreach (
            var same in live.GroupBy(x => (x.Entry.Set, x.Launch.Key, x.Launch.Version))
                .Where(g => g.Skip(1).Any())
        )
        {
            var copies = same.ToList();
            var kept = copies.OrderBy(x => KeepRank(x.Entry.Scope)).First();
            var id = groups.Next();
            foreach (var (entry, launch) in copies)
            {
                var keep = ReferenceEquals(entry, kept.Entry);
                var detail = keep
                    ? $"runs {copies.Count} times; keep this copy ({Why(kept.Entry.Scope)})"
                    : $"runs {copies.Count} times; an extra copy";
                yield return Of(
                    entry,
                    launch,
                    ProblemKinds.DuplicateMcp,
                    detail,
                    !keep && entry.Scope != Scope.Plugin,
                    id,
                    keep
                );
            }
        }
    }

    /// <summary>One package pinned at several versions: different servers, worth knowing.</summary>
    private static IEnumerable<Problem> Versions(
        List<(McpEntry Entry, Launch Launch)> live,
        GroupCounter groups
    )
    {
        foreach (var same in live.GroupBy(x => (x.Entry.Set, x.Launch.Key)))
        {
            var versions = same.Select(x => x.Launch.Version)
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (versions < 2)
            {
                continue;
            }
            var id = groups.Next();
            foreach (var (entry, launch) in same)
            {
                var detail =
                    $"same package at {versions} versions, not duplicates; this one is {launch.Version ?? "unpinned"}";
                yield return Of(
                    entry,
                    launch,
                    ProblemKinds.ConflictingMcp,
                    detail,
                    false,
                    id,
                    false
                );
            }
        }
    }

    /// <summary>
    /// rtok configured more than once on one surface: a copy under another name launches rtok
    /// twice and may go; one named <c>rtok</c> in another scope or a plugin is information only.
    /// </summary>
    private static IEnumerable<Problem> Own(
        List<(McpEntry Entry, Launch Launch)> copies,
        GroupCounter groups
    )
    {
        var kept = copies
            .OrderBy(x => !string.Equals(x.Entry.Name, "rtok", StringComparison.Ordinal))
            .ThenBy(x => x.Entry.Scope != Scope.User)
            .ThenBy(x => KeepRank(x.Entry.Scope))
            .First();
        var rest = copies.Where(x => !ReferenceEquals(x.Entry, kept.Entry)).ToList();
        var extra = rest.Where(x => !IsRtokName(x.Entry) && x.Entry.Scope != Scope.Plugin).ToList();
        var info = rest.Where(x => IsRtokName(x.Entry) || x.Entry.Scope == Scope.Plugin).ToList();
        return
        [
            .. Extras(kept, extra, groups),
            .. Info(copies, kept, info, extra.Count == 0, groups),
        ];
    }

    /// <summary>The kept copy and the extra copies under other names, one group.</summary>
    private static IEnumerable<Problem> Extras(
        (McpEntry Entry, Launch Launch) kept,
        List<(McpEntry Entry, Launch Launch)> extra,
        GroupCounter groups
    )
    {
        if (extra.Count > 0)
        {
            var id = groups.Next();
            var n = extra.Count + 1;
            yield return Of(
                kept.Entry,
                kept.Launch,
                ProblemKinds.DuplicateMcp,
                $"runs {n} times; keep this copy (rtok's own entry, install writes it)",
                false,
                id,
                true
            );
            foreach (var (entry, launch) in extra)
            {
                yield return Of(
                    entry,
                    launch,
                    ProblemKinds.DuplicateMcp,
                    $"runs {n} times; an extra copy that also launches rtok",
                    true,
                    id,
                    false
                );
            }
        }
    }

    /// <summary>The copies named <c>rtok</c> elsewhere or in a plugin: information only.</summary>
    private static IEnumerable<Problem> Info(
        List<(McpEntry Entry, Launch Launch)> copies,
        (McpEntry Entry, Launch Launch) kept,
        List<(McpEntry Entry, Launch Launch)> info,
        bool alone,
        GroupCounter groups
    )
    {
        if (info.Count == 0)
        {
            yield break;
        }
        var used =
            copies
                .Where(x => IsRtokName(x.Entry) && x.Entry.Scope != Scope.Plugin)
                .OrderByDescending(x => x.Entry.Wins)
                .Select(x => Why(x.Entry.Scope))
                .FirstOrDefault()
            ?? "the config";
        var group = groups.Next();
        var said = $"rtok is configured {info.Count + 1} times; information only";
        if (alone)
        {
            yield return Of(
                kept.Entry,
                kept.Launch,
                ProblemKinds.OwnMcp,
                $"{said}; this is the entry install writes, kept",
                false,
                group,
                true
            );
        }
        foreach (var (entry, launch) in info)
        {
            var why =
                entry.Scope == Scope.Plugin
                    ? $"the host shadows or merges the plugin copy; `rtok agents update {entry.Agent}` brings it up to date"
                    : $"the host uses the {used} entry named `rtok` and merges the rest";
            yield return Of(
                entry,
                launch,
                ProblemKinds.OwnMcp,
                $"{said}: {why}",
                false,
                group,
                false
            );
        }
    }

    private static bool IsRtokName(McpEntry e) =>
        string.Equals(e.Key, "rtok", StringComparison.Ordinal);

    /// <summary>The entry launches rtok: any string in it names the rtok binary.</summary>
    internal static bool IsOwn(JsonNode? node, DoctorHome home) =>
        node switch
        {
            JsonObject o => o.Any(kv => IsOwn(kv.Value, home)),
            JsonArray a => a.Any(x => IsOwn(x, home)),
            _ => DoctorHome.Str(node) is { } s && home.Options.Rtok.Is(s),
        };

    /// <summary>How the entry starts; null when it is turned off or names nothing to start.</summary>
    internal static Launch? LaunchOf(JsonObject e, DoctorHome home)
    {
        if (Is(e["disabled"], JsonValueKind.True) || Is(e["enabled"], JsonValueKind.False))
        {
            return null;
        }
        var env = e["env"] is JsonObject vars
            ? string.Join(
                '\u001f',
                vars.Select(kv => $"{kv.Key}={Raw(kv.Value)}").Order(StringComparer.Ordinal)
            )
            : "";
        foreach (var key in (string[])["url", "serverUrl", "httpUrl"])
        {
            if (DoctorHome.Str(e[key]) is { Length: > 0 } url)
            {
                return new Launch(
                    Join("url", NormalUrl(url), DoctorHome.Str(e["type"]) ?? "", env),
                    null,
                    url
                );
            }
        }
        var argv = Argv(e);
        if (argv.Count == 0)
        {
            return null;
        }
        var shown = string.Join(' ', argv);
        var program = CommandCheck.Base(argv[0]);
        if (Runners.Contains(program))
        {
            var at = argv.FindIndex(1, a => !a.StartsWith('-'));
            if (at > 0)
            {
                var spec = argv[at];
                var split = spec.LastIndexOf('@');
                var (pkg, version) =
                    split > 0 ? (spec[..split], spec[(split + 1)..]) : (spec, null);
                return new Launch(Join([program, pkg, .. argv[(at + 1)..], env]), version, shown);
            }
        }
        var path = argv[0].AsSpan().ContainsAny('/', '\\')
            ? home.Hosts.Probe.Expand(argv[0])
            : home.Hosts.Probe.FindOnPath(argv[0]) ?? argv[0];
        return new Launch(Join([home.Canonical(path), .. argv[1..], env]), null, shown);
    }

    /// <summary>The command (a string, or an array as OpenCode writes it) and its args.</summary>
    private static List<string> Argv(JsonObject e)
    {
        var argv = new List<string>();
        void Add(JsonNode? node)
        {
            if (DoctorHome.Str(node) is { } s)
            {
                argv.Add(s);
            }
            else if (node is JsonArray a)
            {
                argv.AddRange(a.Select(DoctorHome.Str).OfType<string>());
            }
        }
        Add(e["command"]);
        Add(e["args"]);
        return argv;
    }

    private static string NormalUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped).TrimEnd('/')
            : url.ToLowerInvariant();

    private static string Raw(JsonNode? node) => DoctorHome.Str(node) ?? node?.ToJsonString() ?? "";

    private static bool Is(JsonNode? node, JsonValueKind kind) =>
        node is JsonValue v && v.GetValueKind() == kind;

    private static string Join(params IEnumerable<string> parts) => string.Join('\u001e', parts);

    /// <summary>Which copy of one launch stays: plugin, then project, user, local.</summary>
    private static int KeepRank(Scope scope) =>
        scope switch
        {
            Scope.Plugin => 0,
            Scope.Project => 1,
            Scope.User => 2,
            _ => 3,
        };

    private static string Why(Scope scope) =>
        scope switch
        {
            Scope.Project => "shared project file",
            Scope.User => "user file",
            Scope.Local => "local scope",
            _ => "enabled plugin",
        };

    private static Problem Of(
        McpEntry e,
        Launch launch,
        string kind,
        string detail,
        bool fixable,
        int group,
        bool keep
    ) =>
        new(kind, e.Agent, e.Source, e.Path, detail, fixable)
        {
            Command = launch.Shown,
            Group = group,
            Keep = keep,
            Editable = e.Scope != Scope.Plugin,
        };
}
