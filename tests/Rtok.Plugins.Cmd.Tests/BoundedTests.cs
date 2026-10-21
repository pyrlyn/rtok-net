namespace Rtok.Plugins.Cmd.Tests;

/// <summary>Bounded commands pass through; mixed chains take <c>[script]</c>; the quote-aware lexer.</summary>
public sealed class BoundedTests
{
    [Fact]
    public void Host_visible_prefix_stops_at_thirty_thousand_scalars()
    {
        var over = new string('x', Bounded.HostVisibleChars + 1);
        Assert.Equal(new string('x', Bounded.HostVisibleChars), Bounded.HostVisiblePrefix(over));

        var exact = string.Concat(Enumerable.Repeat("é", Bounded.HostVisibleChars));
        Assert.Equal(exact, Bounded.HostVisiblePrefix(exact));

        // Scalars, not UTF-16 units: an astral character is one and is never split.
        var astral = string.Concat(Enumerable.Repeat("😀", Bounded.HostVisibleChars + 1));
        var prefix = Bounded.HostVisiblePrefix(astral);
        Assert.Equal(Bounded.HostVisibleChars * 2, prefix.Length);
    }

    [Theory]
    [InlineData("sed -n '1,620p' src/hooks/types.rs")]
    [InlineData("sed -n 10p a; sed -n -e 1,5p -e 9p b")]
    [InlineData("cargo nextest run --workspace 2>&1 | tail -300")]
    [InlineData("cargo nextest run |& tail -n 50")]
    [InlineData("git log --oneline | head")]
    [InlineData("head -n 40 a.rs b.rs")]
    [InlineData("grep -rn -A3 -m 5 'a|b' src")]
    [InlineData("rg -C 2 needle")]
    [InlineData("grep -m 5 x f")]
    [InlineData("grep -A3 x f")]
    [InlineData("cd /repo && sed -n '1,80p' Cargo.toml")]
    [InlineData("cat -n src/main.rs")]
    [InlineData("cat -n one.txt")]
    [InlineData("head -n 5 log")]
    [InlineData("tail -n 5 log")]
    [InlineData("sed -n 1,9p a.rs")]
    [InlineData("rg -m 3 needle")]
    // `rg` is recursive by default and `-r` is `--replace`; the context flag still bounds it.
    [InlineData("rg -r -C 2 needle src")]
    public void Bounded_forms(string cmd) => Assert.True(Bounded.IsBounded(cmd), cmd);

    [Theory]
    [InlineData("cargo nextest run")]
    [InlineData("sed -n '/fn main/,$p' a.rs")]
    [InlineData("sed 's/a/b/' a.rs")]
    [InlineData("tail -n +5 log")]
    [InlineData("tail -f log")]
    [InlineData("tail +5 log")]
    [InlineData("head -n -5 log")]
    [InlineData("grep -rn needle src")]
    [InlineData("sed -n 1,5p a | grep x")]
    [InlineData("make && tail -5 build.log")]
    [InlineData("cat a.rs")]
    [InlineData("cd /repo")]
    [InlineData("echo 'x | head'")]
    // Context flags bound lines per match, not the match count.
    [InlineData("grep -rn -A3 'a|b' src")]
    // `-n` only numbers lines; several named files are still an unbounded dump.
    [InlineData("cat -n a.rs b.rs")]
    public void Unbounded_forms(string cmd) => Assert.False(Bounded.IsBounded(cmd), cmd);

    [Theory]
    [InlineData("git status")]
    [InlineData("cargo build && cargo test")]
    [InlineData("cd x && cargo test")]
    [InlineData("cargo test; cargo clippy")]
    [InlineData("cargo test \\\n  --all")]
    [InlineData("mise exec -- cargo build && mise exec -- cargo test")]
    public void Same_program_forms(string cmd) => Assert.False(Bounded.MixedChain(cmd), cmd);

    [Theory]
    [InlineData("cargo test && cargo clippy && git status")]
    [InlineData("mise exec -- cargo test && mise exec -- git status")]
    [InlineData("git log | grep foo")]
    [InlineData("echo one; cat two")]
    [InlineData("git status\ncat file.txt\nls")]
    public void Mixed_program_forms(string cmd) => Assert.True(Bounded.MixedChain(cmd), cmd);

    // `\` outside quotes makes the next char literal; inside double quotes it escapes only
    // `"`, `\`, `$` and backtick; single quotes have no escapes.
    [Theory]
    [InlineData(@"echo it\'s && cd sub", "echo|it's;cd|sub")]
    [InlineData("echo \"a\\\" b\" && x", "echo|a\" b;x")]
    [InlineData("echo \"a\\\\\" && x", "echo|a\\;x")]
    [InlineData("echo \"a\\n\" && x", "echo|a\\n;x")]
    [InlineData(@"echo 'a\' && x", "echo|a\\;x")]
    [InlineData(@"echo a\;b c", "echo|a;b|c")]
    [InlineData("cargo test \\\n --all", "cargo|test|--all")]
    [InlineData("git log | grep -n 'a b'", "git|log;grep|-n|a b")]
    [InlineData("a || b; c\nd", "a;b;c;d")]
    [InlineData("''", "")]
    public void Lexer_stages(string cmd, string want) =>
        Assert.Equal(want, string.Join(';', Bounded.Stages(cmd).Select(s => string.Join('|', s))));
}
