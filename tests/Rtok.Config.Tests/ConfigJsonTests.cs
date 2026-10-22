using System.Text.Json;

namespace Rtok.Config.Tests;

public sealed class ConfigJsonTests
{
    [Fact]
    public void A_section_is_an_object_with_its_leaves_typed()
    {
        using var box = new Sandbox();
        box.Write(
            "user/.rtok/config.toml",
            "[plugins.inject]\nbudget_tokens = 300\nmodes = [\"terse\", \"yagni\"]\n[estimator]\nprose = 4.5\n"
        );
        var config = box.Load();

        var inject = config.Json("plugins.inject");

        Assert.NotNull(inject);
        Assert.Equal(JsonValueKind.Object, inject.Value.ValueKind);
        Assert.Equal(300, inject.Value.GetProperty("budget_tokens").GetInt32());
        Assert.Equal(
            ["terse", "yagni"],
            inject.Value.GetProperty("modes").EnumerateArray().Select(m => m.GetString()),
            StringComparer.Ordinal
        );
        Assert.Equal(4.5, config.Json("estimator.prose")!.Value.GetDouble());
    }

    [Fact]
    public void A_leaf_and_a_missing_key()
    {
        using var box = new Sandbox();
        var config = box.Load();

        Assert.Equal(JsonValueKind.True, config.Json("hook.fail_open")!.Value.ValueKind);
        Assert.Equal(10, config.Json("hook.max_ms")!.Value.GetInt32());
        Assert.Null(config.Json("plugins.nope"));
    }
}
