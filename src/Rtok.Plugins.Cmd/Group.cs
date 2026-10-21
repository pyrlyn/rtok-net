namespace Rtok.Plugins.Cmd;

/// <summary>How a rule folds related lines before the cut.</summary>
internal enum Group
{
    /// <summary>No folding.</summary>
    Off,

    /// <summary>Path lines fold per directory: <c>src/ (12 files): a.rs, b.rs, c.rs …</c>.</summary>
    Dir,

    /// <summary>Diagnostics fold per code: <c>E0308 ×3: mismatched types (a.rs:1, …)</c>.</summary>
    Diag,
}
