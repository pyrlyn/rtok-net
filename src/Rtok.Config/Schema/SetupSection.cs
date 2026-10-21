namespace Rtok.Config;

/// <summary>rtok agents install / remove &lt;host&gt;</summary>
/// <remarks>Read from [setup].</remarks>
public sealed class SetupSection
{
    /// <summary>print what install would change and write nothing</summary>
    public bool DryRun { get; set; }

    /// <summary>required by --replace</summary>
    public bool Yes { get; set; }

    /// <summary>_backup/&lt;name&gt;.bak-&lt;ts&gt; beside each file, before install and uninstall touch it</summary>
    public bool Backup { get; set; } = true;

    /// <summary>.bak-* generations kept per file; older ones are deleted (0 = keep all)</summary>
    public uint BackupFiles { get; set; } = 5;

    /// <summary>timeout written into each hook entry</summary>
    public ulong HookTimeoutS { get; set; } = 5;

    /// <summary>e.g. ["terse", "yagni"] (--mode)</summary>
    public IList<string> Modes { get; set; } = [];

    /// <summary>also register the MCP server (--mcp)</summary>
    public bool Mcp { get; set; } = true;

    /// <summary>also set the base URL (--proxy)</summary>
    public bool Proxy { get; set; }

    /// <summary>`agents update --force`: reinstall the host plugin even when the version decision would otherwise skip it</summary>
    public bool Force { get; set; }

    /// <summary>`agents update --source github|local|marketplace`: override the install source the version decision compares against, instead of the receipt's recorded one</summary>
    public string? Source { get; set; }

    /// <summary>The [setup.claude] table.</summary>
    public SetupClaudeSection Claude { get; set; } = new();

    /// <summary>The [setup.cursor] table.</summary>
    public SetupCursorSection Cursor { get; set; } = new();

    /// <summary>The [setup.codex] table.</summary>
    public SetupCodexSection Codex { get; set; } = new();

    /// <summary>The [setup.opencode] table.</summary>
    public SetupOpenCodeSection Opencode { get; set; } = new();

    /// <summary>The [setup.kilo] table.</summary>
    public SetupKiloSection Kilo { get; set; } = new();

    /// <summary>The [setup.pi] table.</summary>
    public SetupPiSection Pi { get; set; } = new();

    /// <summary>The [setup.omp] table.</summary>
    public SetupOmpSection Omp { get; set; } = new();

    /// <summary>The [setup.zcode] table.</summary>
    public SetupZcodeSection Zcode { get; set; } = new();

    /// <summary>The [setup.kimi] table.</summary>
    public SetupKimiSection Kimi { get; set; } = new();

    /// <summary>The [setup.grok] table.</summary>
    public SetupGrokSection Grok { get; set; } = new();

    /// <summary>The [setup.vscode] table.</summary>
    public SetupVscodeSection Vscode { get; set; } = new();

    /// <summary>The [setup.copilot] table.</summary>
    public SetupCopilotSection Copilot { get; set; } = new();

    /// <summary>The [setup.commandcode] table.</summary>
    public SetupCommandCodeSection Commandcode { get; set; } = new();

    /// <summary>The [setup.aider] table.</summary>
    public SetupAiderSection Aider { get; set; } = new();

    /// <summary>The [setup.windsurf] table.</summary>
    public SetupWindsurfSection Windsurf { get; set; } = new();

    /// <summary>The [setup.zed] table.</summary>
    public SetupZedSection Zed { get; set; } = new();

    /// <summary>The [setup.cline] table.</summary>
    public SetupClineSection Cline { get; set; } = new();

    /// <summary>The [setup.gemini] table.</summary>
    public SetupGeminiSection Gemini { get; set; } = new();

    /// <summary>The [setup.codewhale] table.</summary>
    public SetupCodewhaleSection Codewhale { get; set; } = new();

    /// <summary>The [setup.mimo] table.</summary>
    public SetupMimoSection Mimo { get; set; } = new();

    /// <summary>The [setup.antigravity] table.</summary>
    public SetupAntigravitySection Antigravity { get; set; } = new();

    /// <summary>The [setup.devin] table.</summary>
    public SetupDevinSection Devin { get; set; } = new();

    /// <summary>The [setup.roo] table.</summary>
    public SetupRooSection Roo { get; set; } = new();

    /// <summary>The [setup.junie] table.</summary>
    public SetupJunieSection Junie { get; set; } = new();

    /// <summary>The [setup.qwen] table.</summary>
    public SetupQwenSection Qwen { get; set; } = new();

    /// <summary>The [setup.codebuddy] table.</summary>
    public SetupCodebuddySection Codebuddy { get; set; } = new();

    /// <summary>The [setup.goose] table.</summary>
    public SetupGooseSection Goose { get; set; } = new();

    /// <summary>The [setup.droid] table.</summary>
    public SetupDroidSection Droid { get; set; } = new();

    /// <summary>The [setup.kiro] table.</summary>
    public SetupKiroSection Kiro { get; set; } = new();

    /// <summary>The [setup.amp] table.</summary>
    public SetupAmpSection Amp { get; set; } = new();

    /// <summary>The [setup.warp] table.</summary>
    public SetupWarpSection Warp { get; set; } = new();

    /// <summary>The [setup.continue] table.</summary>
    public SetupContinueSection Continue { get; set; } = new();

    /// <summary>The [setup.crush] table.</summary>
    public SetupCrushSection Crush { get; set; } = new();

    /// <summary>The [setup.openhands] table.</summary>
    public SetupOpenhandsSection Openhands { get; set; } = new();

    /// <summary>The [setup.augment] table.</summary>
    public SetupAugmentSection Augment { get; set; } = new();

    /// <summary>The [setup.reasonix] table.</summary>
    public SetupReasonixSection Reasonix { get; set; } = new();
}
