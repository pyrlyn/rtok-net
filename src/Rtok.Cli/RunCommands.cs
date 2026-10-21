using ConsoleAppFramework;
using Rtok.Config;
#if RTOK_PLUGIN_CMD
using Rtok.Plugins.Cmd;
#endif

namespace Rtok.Cli;

/// <summary>
/// <c>rtok run</c> and <c>rtok filter</c>, the cmd plugin's commands. A build without that
/// plugin keeps them: <c>run</c> fails and <c>filter</c> passes stdin through.
/// </summary>
internal static class RunCommands
{
    /// <summary>Run a shell command, archive its output and print it filtered; exits with the command's code.</summary>
    /// <param name="context">The arguments after <c>--</c> are the command.</param>
    /// <param name="cancellationToken">Kills the command.</param>
    /// <param name="agent">Sub-agent id from PreToolUse; scopes the dedup pointer.</param>
    /// <param name="noRef">Do not print the reference line after a cut.</param>
    /// <param name="config">The config file to load instead of the user's.</param>
    /// <param name="command">The command, when it starts with no option; otherwise after --.</param>
    public static async Task<int> RunAsync(
        ConsoleAppContext context,
        CancellationToken cancellationToken,
        string? agent = null,
        bool noRef = false,
        string? config = null,
        [Argument] params string[] command
    )
    {
        string[] words = [.. command, .. context.EscapedArguments];
#if RTOK_PLUGIN_CMD
        if (words.Length == 0)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException(
                "missing command: rtok run -- <command>"
            );
        }
        var section = Load(config, noRef).Plugins.Cmd;
        using var stdout = Console.OpenStandardOutput();
        // No store host until T9: the output goes out raw (fail open).
        return await CmdCommands
            .RunAsync(
                section,
                words,
                agent,
                host: null,
                stdout,
                Environment.GetEnvironmentVariable,
                cancellationToken
            )
            .ConfigureAwait(false);
#else
        _ = (agent, noRef, config, cancellationToken, words);
        await Console
            .Error.WriteLineAsync("rtok run: not implemented (built without the cmd plugin)")
            .ConfigureAwait(false);
        return 1;
#endif
    }

    /// <summary>Filter stdin as the output of a command, without running it.</summary>
    /// <param name="stdin">Read the payload from stdin (OpenCode tool.execute.after).</param>
    /// <param name="cmd">Command family hint (git status, cargo test, …).</param>
    /// <param name="archive">Archive stdin and print the expand trailer (same path as run).</param>
    /// <param name="noRef">Do not print the reference line after a cut.</param>
    /// <param name="exit">Exit code of the command that printed stdin (with --archive); formatters read it.</param>
    /// <param name="config">The config file to load instead of the user's.</param>
    public static void Filter(
        bool stdin = false,
        string? cmd = null,
        bool archive = false,
        bool noRef = false,
        [Hidden] int exit = 0,
        string? config = null
    )
    {
        // stdin is always the payload; the flag stays for the callers that pass it.
        _ = stdin;
        byte[] input;
        using (var buffer = new MemoryStream())
        {
            using var from = Console.OpenStandardInput();
            from.CopyTo(buffer);
            input = buffer.ToArray();
        }
        using var stdout = Console.OpenStandardOutput();
#if RTOK_PLUGIN_CMD
        var loaded = Load(config, noRef);
        CmdCommands.Filter(
            loaded.Plugins.Cmd,
            cmd ?? loaded.Filter.Cmd,
            input,
            archive,
            exit,
            host: null,
            stdout,
            Environment.GetEnvironmentVariable
        );
#else
        _ = (cmd, archive, noRef, exit, config);
        stdout.Write(input);
#endif
    }

#if RTOK_PLUGIN_CMD
    private static RtokConfig Load(string? config, bool noRef) =>
        ConfigLoader
            .Load(
                new ConfigLoadOptions
                {
                    ConfigFile = config,
                    Flags = noRef ? [new ConfigFlag("plugins.cmd.trailer", false, "--no-ref")] : [],
                }
            )
            .Config;
#endif
}
