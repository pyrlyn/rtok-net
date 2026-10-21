namespace Rtok.Config;

/// <summary>rtok worktree add | list | gc</summary>
/// <remarks>Read from [worktree].</remarks>
public sealed class WorktreeSection
{
    /// <summary>false: rtok worktree and MCP worktree_* are off; Claude's worktree hooks act as without rtok</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>where `rtok worktree add` creates worktrees, as &lt;root&gt;/&lt;repo&gt;-&lt;task&gt;; relative: under the main checkout</summary>
    public string Root { get; set; } = ".rtok/worktrees";
}
