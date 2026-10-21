namespace Rtok.Agents;

/// <summary>The names people know the hosts by: the CLI variant's, else the host's first variant.</summary>
public static class HostNames
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.Ordinal)
    {
        ["aider"] = "aider",
        ["amp"] = "Amp",
        ["antigravity"] = "Antigravity CLI",
        ["augment"] = "Augment Auggie CLI",
        ["claude"] = "Claude Code",
        ["cline"] = "Cline CLI",
        ["codebuddy"] = "CodeBuddy Code",
        ["codewhale"] = "CodeWhale",
        ["codex"] = "Codex",
        ["commandcode"] = "Command Code CLI",
        ["continue"] = "Continue CLI",
        ["copilot"] = "Copilot CLI",
        ["crush"] = "Crush",
        ["cursor"] = "Cursor CLI",
        ["devin"] = "Devin CLI",
        ["droid"] = "Factory Droid",
        ["gemini"] = "Gemini CLI",
        ["goose"] = "Goose CLI",
        ["grok"] = "Grok Build",
        ["junie"] = "Junie CLI",
        ["kilo"] = "Kilo Code CLI",
        ["kimi"] = "Kimi Code",
        ["kiro"] = "Kiro CLI",
        ["mimo"] = "MiMo Code",
        ["omp"] = "oh my pi",
        ["opencode"] = "OpenCode",
        ["openhands"] = "OpenHands",
        ["pi"] = "pi",
        ["qwen"] = "Qwen Code",
        ["reasonix"] = "Reasonix",
        ["roo"] = "Roo Code",
        ["vscode"] = "VS Code",
        ["warp"] = "Warp",
        ["windsurf"] = "Windsurf",
        ["zcode"] = "ZCode",
        ["zed"] = "Zed CLI",
    };

    /// <summary>The name of <paramref name="host"/>, else the id itself.</summary>
    public static string Of(string host) => Names.GetValueOrDefault(host, host);
}
