using Rtok.Hooks.Events;

namespace Rtok.Hooks.Tests.Events;

public sealed class HookEventsTests
{
    public static TheoryData<string> ManifestHosts() =>
        new(HookManifests.Files.Select(f => f.Host));

    [Fact]
    public void No_row_is_listed_twice()
    {
        var rows = HookEvents.All;

        Assert.Equal(rows.Count, rows.Distinct().Count());
    }

    // A host event (with its matcher) runs one rtok event, and an rtok event (with its matcher)
    // answers one host event.
    [Fact]
    public void Each_host_maps_an_event_to_one_name()
    {
        foreach (var host in HookEvents.Hosts)
        {
            var rows = HookEvents.ForHost(host);
            Assert.All(
                rows.GroupBy(r => (r.HostEvent, r.Matcher)),
                g => Assert.Single(g.Select(r => r.RtokEvent).Distinct(StringComparer.Ordinal))
            );
            Assert.All(
                rows.GroupBy(r => (r.RtokEvent, r.Matcher)),
                g => Assert.Single(g.Select(r => r.HostEvent).Distinct(StringComparer.Ordinal))
            );
        }
    }

    [Fact]
    public void Every_host_with_hooks_has_a_manifest_or_scripts()
    {
        var covered = HookManifests.Files.Select(f => f.Host).Concat(HookManifests.ScriptHosts);

        Assert.Equal(
            HookEvents.Hosts.Order(StringComparer.Ordinal),
            covered.Order(StringComparer.Ordinal)
        );
    }

    [Theory]
    [MemberData(nameof(ManifestHosts))]
    public Task The_manifest_is_generated_from_the_table(string host) =>
        Verify(HookManifests.For(host), extension: "json").UseParameters(host);

    [Fact]
    public Task The_script_hosts_link_one_script_per_host_event() =>
        Verify(
            HookManifests.ScriptHosts.ToDictionary(
                h => h,
                HookManifests.ScriptEvents,
                StringComparer.Ordinal
            )
        );
}
