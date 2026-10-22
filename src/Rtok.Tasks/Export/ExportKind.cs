namespace Rtok.Tasks.Export;

/// <summary>What an export does to one task's issue.</summary>
public enum ExportKind
{
    /// <summary>Open an issue for it.</summary>
    Create,

    /// <summary>Rewrite the fields that drifted.</summary>
    Update,

    /// <summary>The task is finished locally and the issue is still open.</summary>
    Close,

    /// <summary>The task is active locally and the issue is finished.</summary>
    Reopen,
}
