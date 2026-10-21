using Rtok.Config;

namespace Rtok.Model.Tests;

public sealed class ActionTests
{
    [Fact]
    public void The_plugin_toggle_plans_the_same_write_as_config_set_and_writes_nothing_before_apply()
    {
        using var fx = new ModelFixture();
        var model = fx.Model();
        var before = File.ReadAllText(fx.UserFile);

        var plan = model.PlanPluginToggle("docs", enabled: false);

        var cli = ConfigFile.PlanSet(fx.UserFile, "plugins.docs.enabled", "false", fx.Options);
        Assert.Equal(cli, plan);
        Assert.Equal(before, File.ReadAllText(fx.UserFile));
        Assert.Contains("+enabled = false", plan.Diff(), StringComparison.Ordinal);
    }

    [Fact]
    public void Applying_the_toggle_writes_the_plan_and_rebuilds()
    {
        using var fx = new ModelFixture();
        using var built = new SemaphoreSlim(0);
        var model = fx.Model(built: _ => built.Release());

        var plan = model.PlanPluginToggle("docs", enabled: false);
        model.Apply(plan);

        Assert.Equal(plan.After, File.ReadAllText(fx.UserFile));
        Assert.True(built.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.False(Assert.Single(model.Current!.Plugins, p => p is { Id: "docs" }).Enabled);
    }

    [Fact]
    public void A_stale_plan_is_refused()
    {
        using var fx = new ModelFixture();
        var model = fx.Model();
        var plan = model.PlanPluginToggle("measure", enabled: false);
        File.AppendAllText(fx.UserFile, "# edited\n");

        var e = Assert.Throws<ConfigException>(() => model.Apply(plan));
        Assert.Contains("plan again", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_a_linked_plugin_can_be_toggled()
    {
        using var fx = new ModelFixture();

        var e = Assert.Throws<ConfigException>(() => fx.Model().PlanPluginToggle("guard", true));
        Assert.Equal("no such plugin: guard", e.Message);
    }
}
