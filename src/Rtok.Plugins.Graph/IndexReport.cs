namespace Rtok.Plugins.Graph;

/// <summary>What one index run did.</summary>
public sealed class IndexReport
{
    /// <summary>Files whose rows were (or, in a dry run, would be) replaced.</summary>
    public int Indexed { get; internal set; }

    /// <summary>Rows written.</summary>
    public int Inserted { get; internal set; }

    /// <summary>Files left alone: the stat or the sha matched.</summary>
    public int Skipped { get; internal set; }

    /// <summary>Files whose bytes were read. A warm run over an untouched tree reads none.</summary>
    public int Read { get; internal set; }

    /// <summary>Files with a built-in grammar dropped by <c>exclude</c>.</summary>
    public int ExcludeSkipped { get; internal set; }

    /// <summary>Files indexed only because <c>include</c> brought them back.</summary>
    public int IncludeAdded { get; internal set; }

    /// <summary>Files indexed only because <c>extensions</c> maps their suffix.</summary>
    public int ExtensionMapped { get; internal set; }

    /// <inheritdoc/>
    public override string ToString() =>
        $"indexed {Indexed}, inserted {Inserted}, skipped {Skipped}, read {Read}";
}
