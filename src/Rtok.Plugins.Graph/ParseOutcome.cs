namespace Rtok.Plugins.Graph;

/// <summary>What a worker made of a <see cref="ParseJob"/>.</summary>
internal enum ParseOutcome
{
    /// <summary>The read failed (permissions, I/O); the bytes may be fine next time.</summary>
    Unreadable,

    /// <summary>Read, but not UTF-8; stays so until the bytes change.</summary>
    NotText,

    /// <summary>Read, but the grammar's query failed.</summary>
    Unparsed,

    /// <summary>The same bytes as the stored sha.</summary>
    Same,

    /// <summary>New rows.</summary>
    Rows,
}
