using Rtok.Agents.Hosts.Mcp;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any host config text through the text-splicing adapters <c>agents install</c> writes with
/// (Goose and Continue YAML, Reasonix TOML), installed and removed as <c>McpFile.Apply</c> does: a
/// file is refused only with a non-empty <see cref="InvalidDataException"/>; install leaves a
/// current entry and a second install changes nothing; remove leaves no entry.
/// </summary>
internal static class AgentsInstall
{
    private static readonly McpFile[] Files =
    [
        new GooseMcp("/fuzz/.config/goose/config.yaml"),
        new ContinueMcp("/fuzz/.continue/config.yaml"),
        new ReasonixMcp("/fuzz/.reasonix/config.toml"),
    ];

    public static void Run(ReadOnlySpan<byte> data)
    {
        var (pick, text) = FuzzInput.Picked(data);
        var file = Files[pick % Files.Length];
        try
        {
            var installed = Install(file, text);
            FuzzInput.Check(
                file.Entry(installed) is { } have && file.Current(have, "rtok"),
                "install does not leave a current entry"
            );
            FuzzInput.Check(
                string.Equals(Install(file, installed), installed, StringComparison.Ordinal),
                "a second install changes the file"
            );
            FuzzInput.Check(
                file.Entry(Remove(file, installed)) is null,
                "an entry survives remove"
            );
            FuzzInput.Check(
                file.Entry(Remove(file, text)) is null,
                "an entry survives remove of the file as found"
            );
        }
        catch (InvalidDataException e)
        {
            FuzzInput.Check(e.Message.Length > 0, "a refusal without a reason");
        }
    }

    private static string Install(McpFile file, string text) =>
        file.Entry(text) is { } have && file.Current(have, "rtok") ? text : file.With(text, "rtok");

    private static string Remove(McpFile file, string text) =>
        file.Entry(text) is null ? text : file.Without(text);
}
