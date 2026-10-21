using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rtok.Plugin.Sdk;

namespace Rtok.SamplePlugin;

/// <summary>
/// A complete plugin in one file. It watches for a recursive <c>ls</c>, whose output is
/// usually thousands of lines the model did not ask for, and rewrites it to the shallow form.
/// It is the smallest thing that obeys all three rules at once:
/// fail open (anything surprising returns <see langword="null"/> and the command runs
/// unchanged); lossless (the original command is archived before it is replaced, and its id
/// travels in the reason, so <c>rtok expand &lt;id&gt;</c> gives it back); measured (the saving
/// is a <see cref="Measurement"/> row, or it did not happen).
/// </summary>
public sealed class Shrink : IPlugin
{
    /// <inheritdoc/>
    public Manifest Manifest { get; } = new("shrink", Surface.Hook, DefaultOn: true);

    /// <inheritdoc/>
    public DashboardPage DashboardPage { get; } =
        new("Shrink", "Rewrites recursive `ls` to the shallow form.", SavesTokens: true);

    /// <inheritdoc/>
    public PreToolDecision? OnPreTool(PreToolUse ev, Ctx cx)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(cx);
        if (
            !string.Equals(ev.ToolName, "Bash", StringComparison.Ordinal)
            || ev.ToolInput.ValueKind != JsonValueKind.Object
            || !ev.ToolInput.TryGetProperty("command", out var command)
            || command.GetString() is not { } cmd
        )
        {
            return null;
        }

        var words = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words is not ["ls", ..] || !words.Any(IsRecursive))
        {
            return null;
        }

        // Lossless first: nothing is replaced before the original is retrievable.
        var archiveId = cx.Host.PutArchive(Encoding.UTF8.GetBytes(cmd));
        var shallow = string.Join(' ', words.Where(w => !IsRecursive(w)));

        var config = cx.PluginConfig("shrink", SampleJsonContext.Default.ShrinkConfig);
        cx.Host.Record(
            new Measurement
            {
                Plugin = Manifest.Id,
                Kind = "rewrite",
                BeforeBytes = Encoding.UTF8.GetByteCount(cmd),
                AfterBytes = Encoding.UTF8.GetByteCount(shallow),
                // What the recursion would print.
                EstBefore = cx.Host.Estimate(cmd, TextClass.Code) * 200,
                EstAfter = Math.Max(config.AssumedAfterTokens, 1),
                RefId = archiveId,
            }
        );
        cx.Host.Log("info", "plugin", Manifest.Id, $"rewrote \"{cmd}\"");

        return new PreToolDecision.Rewrite(
            new JsonObject { ["command"] = shallow },
            $"shrink: recursive listing dropped; original is expand({archiveId})"
        );
    }

    private static bool IsRecursive(string word) => word is "-R" or "-r";
}
