using System.Reflection;
using System.Text.Json;

namespace Rtok.Model.Tests;

public sealed class SnapshotTests
{
    [Fact]
    public void One_snapshot_from_the_fixture_store_fills_every_page()
    {
        using var fx = new ModelFixture();
        var s = fx.Model().Build();

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(ModelFixture.Now), s.BuiltAt);
        Assert.Equal(
            (100, 10, 20, 5, 1),
            (
                s.Overview.Input,
                s.Overview.CacheCreate,
                s.Overview.CacheRead,
                s.Overview.Output,
                s.Overview.Rows
            )
        );
        Assert.Equal([130L], s.Overview.Turns);
        var day = Assert.Single(s.Overview.Savings);
        Assert.Equal(("2027-01-15", 1L, 150L), (day.Day, day.Rows, day.Saved));
        Assert.Equal(150, day.Plugins["measure"]);

        Assert.Equal(["measure", "docs"], s.Plugins.Select(p => p.Id), StringComparer.Ordinal);
        var docs = s.Plugins[1];
        Assert.True(docs.Enabled); // off by default, on in the user file
        Assert.Equal(("builtin", "fake", true), (docs.Kind, docs.Summary, docs.SavesTokens));
        Assert.Equal(["hook", "mcp"], docs.Surfaces);
        Assert.Equal(new PluginField("rows", "3"), Assert.Single(docs.Fields));

        var call = Assert.Single(s.Calls);
        Assert.Equal(("s1", "proxy", "messages"), (call.Session, call.Surface, call.Name));
        Assert.Equal(("claude", "anthropic", "claude-x"), (call.Host, call.Provider, call.Model));
        Assert.Equal((100L, 5L), (call.Input, call.Output));

        var session = Assert.Single(s.Sessions);
        Assert.Equal(("s1", "claude", "proj"), (session.Id, session.Host, session.Project));
        Assert.Equal((ModelFixture.Now, 100L), (session.LastActivity, session.Input));

        Assert.Contains(s.Config, r => r is { Key: "proxy.port", Value: "1001" });
        TextPage[] text =
        [
            s.Doctor,
            s.Logs,
            s.Memory,
            s.Graph,
            s.Services,
            s.Worktrees,
            s.Hosts,
            s.Stats,
            s.Usage,
            s.Tasks,
        ];
        Assert.All(text, page => Assert.EndsWith("\n", page.Text, StringComparison.Ordinal));
    }

    [Fact]
    public void The_logs_page_is_the_rtok_logs_screen_without_colour()
    {
        using var fx = new ModelFixture();
        Assert.Equal(new TextPage("no logs yet\n", Ready: true), fx.Model().Build().Logs);

        var log = Path.Join(fx.Home, "logs", "rtok.log");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        File.WriteAllText(log, "2027-01-15 08:00:00 INF first\n2027-01-15 08:00:01 ERR second\n");

        Assert.Equal(
            "1 2027-01-15 08:00:01 ERR second\n2 2027-01-15 08:00:00 INF first\n",
            fx.Model().Build().Logs.Text
        );
    }

    [Fact]
    public void The_config_page_lists_key_value_source_rows_with_secrets_redacted()
    {
        using var fx = new ModelFixture();
        var rows = new ConfigPageSource().Read(fx.Context());

        var headers = Assert.Single(rows, r => r is { Key: "otel.headers" });
        Assert.DoesNotContain("secret", headers.Value, StringComparison.Ordinal);
        Assert.Matches(@"^otel\.headers = \S+ \(.+\)$", headers.ToString());
        Assert.DoesNotContain(rows, r => r.Value.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_type_in_the_snapshot_is_plain_data() =>
        Assert.Empty(NotPlain(typeof(Snapshot), []));

    [Fact]
    public void The_snapshot_round_trips_through_its_json_context()
    {
        using var fx = new ModelFixture();
        var snapshot = fx.Model().Build();

        var json = JsonSerializer.Serialize(snapshot, SnapshotJsonContext.Default.Snapshot);
        var back = JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.Snapshot);

        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, SnapshotJsonContext.Default.Snapshot));
        Assert.Contains("\"cacheCreate\":10", json, StringComparison.Ordinal);
    }

    // Records of primitives, strings, enums and read-only lists and string-keyed maps of them.
    private static IEnumerable<string> NotPlain(Type type, HashSet<Type> seen)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (
            type.IsPrimitive
            || type.IsEnum
            || type == typeof(string)
            || type == typeof(DateTimeOffset)
            || !seen.Add(type)
        )
        {
            return [];
        }
        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            var args = type.GetGenericArguments();
            if (def == typeof(IReadOnlyList<>))
            {
                return NotPlain(args[0], seen);
            }
            if (def == typeof(IReadOnlyDictionary<,>) && args[0] == typeof(string))
            {
                return NotPlain(args[1], seen);
            }
            return [type.ToString()];
        }
        var record = type.IsSealed && type.GetMethod("<Clone>$") is not null;
        if (!record || !string.Equals(type.Namespace, "Rtok.Model", StringComparison.Ordinal))
        {
            return [type.ToString()];
        }
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var mutable = properties.Where(p =>
            p.SetMethod is { } set
            && !set
                .ReturnParameter.GetRequiredCustomModifiers()
                .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit))
        );
        return mutable
            .Select(p => $"{type.Name}.{p.Name} is settable")
            .Concat(properties.SelectMany(p => NotPlain(p.PropertyType, seen)))
            .ToList();
    }
}
