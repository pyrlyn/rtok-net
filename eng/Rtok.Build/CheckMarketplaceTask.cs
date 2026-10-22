using System.Text.Json;
using Cake.Common.Diagnostics;
using Cake.Frosting;

namespace Rtok.Build;

/// <summary>
/// Checks the Claude and Codex marketplace catalogs (plan/T3). They live in this repo, so a
/// merge to main publishes them; the hosts read them from GitHub. Each catalog must be valid
/// JSON with one <c>rtok</c> plugin whose source is ./plugins/&lt;host&gt;. <c>--strict</c> also
/// requires that directory to exist (a release; T36 carries the trees over).
/// </summary>
[TaskName("check-marketplace")]
public sealed class CheckMarketplaceTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var strict = context.Flag("strict");
        var failed = false;
        void Check(string host, string file, Func<JsonElement, string?> source)
        {
            var found = Source(Path.Combine(context.Root, file), source);
            string? error = null;
            if (found is null)
                error = $"{file}: no rtok plugin with a source";
            else if (!string.Equals(found, "./plugins/" + host, StringComparison.Ordinal))
                error = $"{file}: rtok source is {found}, expected ./plugins/{host}";
            else if (strict && !Directory.Exists(Path.Combine(context.Root, "plugins", host)))
                error = $"{file}: plugins/{host} does not exist";
            if (error is null)
            {
                context.Information($"{host}: ok ({found})");
            }
            else
            {
                context.Error(error);
                failed = true;
            }
        }

        Check("claude", ".claude-plugin/marketplace.json", s => Text(s));
        Check(
            "codex",
            ".agents/plugins/marketplace.json",
            s =>
                s.ValueKind == JsonValueKind.Object
                && s.TryGetProperty("source", out var kind)
                && Text(kind) is "local"
                && s.TryGetProperty("path", out var path)
                    ? Text(path)
                    : null
        );
        if (failed)
            throw BuildContext.Fail("marketplace catalogs failed the check");
    }

    /// <summary>The source of the plugin named rtok, or null when the file has none.</summary>
    private static string? Source(string file, Func<JsonElement, string?> source)
    {
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllBytes(file));
            if (
                !json.RootElement.TryGetProperty("plugins", out var plugins)
                || plugins.ValueKind != JsonValueKind.Array
            )
                return null;
            return plugins
                .EnumerateArray()
                .Where(p =>
                    p.ValueKind == JsonValueKind.Object
                    && p.TryGetProperty("name", out var n)
                    && Text(n) is "rtok"
                )
                .Select(p => p.TryGetProperty("source", out var s) ? source(s) : null)
                .FirstOrDefault(s => s != null);
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
