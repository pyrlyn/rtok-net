using System.IO.Abstractions;
using System.Text;
using Rtok.Config;
using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Cmd;

/// <summary>
/// <c>rtok run</c> and <c>rtok filter</c> for the CLI surface. With no host there is no
/// archive, so output goes out raw (<c>run</c>) or only compressed (<c>filter</c>).
/// </summary>
public static class CmdCommands
{
    /// <summary>
    /// Runs <paramref name="command"/> through the configured or host shell, writes what the
    /// agent should see to <paramref name="output"/> and returns the command's exit code.
    /// </summary>
    /// <param name="section">The <c>[plugins.cmd]</c> section.</param>
    /// <param name="command">The command words; one word is a shell body as is.</param>
    /// <param name="agent">The sub-agent id the Bash rewrite embedded; scopes the dedup pointer.</param>
    /// <param name="host">The archive and Measurement host, or null.</param>
    /// <param name="output">Where the output goes.</param>
    /// <param name="env">The environment: <c>SHELL</c>, <c>ComSpec</c>, <c>RTOK_HOME</c>.</param>
    /// <param name="ct">Kills the child.</param>
    /// <returns>The command's exit code.</returns>
    public static async Task<int> RunAsync(
        CmdSection section,
        IReadOnlyList<string> command,
        string? agent,
        ICapabilities? host,
        Stream output,
        Func<string, string?> env,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(env);
        if (command.Count == 0)
        {
            throw new ArgumentException("rtok run: missing command", nameof(command));
        }

        var windows = OperatingSystem.IsWindows();
        var shell = Shells.Resolve(section.Shell, env("SHELL"), windows, env("ComSpec"));
        var script = Shells.ScriptFor(Shells.Kind(shell), command);
        var (body, exit) = await Capture
            .RunAsync(shell, script, Capture.Label(command), Capture.Cap, ct)
            .ConfigureAwait(false);
        Emitter(section, env).Emit(host, command, body, exit, agent, output);
        return exit;
    }

    /// <summary>
    /// Compresses <paramref name="input"/> as the output of <paramref name="hint"/> without
    /// running it. With <paramref name="archive"/> it takes the <c>run</c> path: the bytes are
    /// archived as they are and <paramref name="exit"/> reaches the formatters.
    /// </summary>
    /// <param name="section">The <c>[plugins.cmd]</c> section.</param>
    /// <param name="hint">The command family hint, such as <c>git status</c>.</param>
    /// <param name="input">The text.</param>
    /// <param name="archive">Archive and print the expand trailer as <c>run</c> does.</param>
    /// <param name="exit">The exit code of the command that printed <paramref name="input"/>.</param>
    /// <param name="host">The archive and Measurement host, or null.</param>
    /// <param name="output">Where the output goes.</param>
    /// <param name="env">The environment, for <c>~</c> in the rule paths.</param>
    public static void Filter(
        CmdSection section,
        string hint,
        byte[] input,
        bool archive,
        int exit,
        ICapabilities? host,
        Stream output,
        Func<string, string?> env
    )
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(hint);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(env);
        if (archive)
        {
            // No dispatch-time agent reaches this surface (OpenCode's `tool.execute.after`).
            Emitter(section, env).Emit(host, Text.Words(hint), input, exit, null, output);
            return;
        }

        var settings = CmdPlugin.Settings(new FileSystem(), section, env);
        var text = new StdinFilter(settings, section.Trailer, section.TrailerMinLines).Run(
            hint,
            Encoding.UTF8.GetString(input),
            host
        );
        output.Write(Encoding.UTF8.GetBytes(text));
    }

    private static Emitter Emitter(CmdSection section, Func<string, string?> env) =>
        new(
            CmdPlugin.Settings(new FileSystem(), section, env),
            section.Trailer,
            section.TrailerMinLines
        );
}
