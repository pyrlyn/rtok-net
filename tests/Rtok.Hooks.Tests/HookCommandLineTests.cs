namespace Rtok.Hooks.Tests;

public sealed class HookCommandLineTests
{
    [Fact]
    public void The_line_prefers_rtok_hook_then_rtok() =>
        Assert.Equal(
            "command -v rtok-hook >/dev/null 2>&1 && exec rtok-hook PreToolUse --host cursor; "
                + "command -v rtok >/dev/null 2>&1 && exec rtok hook PreToolUse --host cursor; "
                + "[ -x \"$HOME/.ketch/bin/rtok-hook\" ] && exec \"$HOME/.ketch/bin/rtok-hook\" PreToolUse --host cursor; "
                + "[ -x \"$HOME/.ketch/bin/rtok\" ] && exec \"$HOME/.ketch/bin/rtok\" hook PreToolUse --host cursor; "
                + "exit 0",
            HookCommandLine.Posix("PreToolUse", "cursor")
        );

    [Fact]
    public void The_line_has_no_brace_group() =>
        Assert.DoesNotContain('{', HookCommandLine.Posix("Stop"));

    [Theory]
    [InlineData("")]
    [InlineData("Pre; rm -rf ~")]
    [InlineData("$(x)")]
    public void Arguments_must_be_plain_words(string word) =>
        Assert.Throws<ArgumentException>(() => HookCommandLine.Posix(word));
}
