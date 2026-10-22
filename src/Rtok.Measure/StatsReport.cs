namespace Rtok.Measure;

/// <summary>
/// What <c>rtok stats</c> reports over a window of Claude Code transcripts. Tool-result tokens
/// are bytes / 4, so the report compares with that baseline; usage counters are the API's own.
/// </summary>
internal sealed class StatsReport
{
    public long Sessions { get; set; }

    /// <summary>Compaction events (<c>subtype=compact_boundary</c>).</summary>
    public long Compact { get; set; }

    /// <summary>Sessions with a <c>checkpoint:</c> or <c>session:</c> note.</summary>
    public long Checkpoint { get; set; }
    public long NoCheckpoint { get; set; }

    /// <summary>File stems of the counted sessions, matched against notes; not in JSON.</summary>
    public List<string> SessionStems { get; } = [];
    public long Lines { get; set; }
    public long Malformed { get; set; }
    public SizeRows Tools { get; } = new();
    public SizeRows BashFamilies { get; } = new();

    /// <summary><c>formatter</c>, a rule name, or <c>default</c> per Bash family.</summary>
    public SortedDictionary<string, string> BashFilter { get; } = new(StringComparer.Ordinal);

    /// <summary><c>cmd</c> rule measurements on families still on the default rule.</summary>
    public SizeRows BashDefaultRule { get; } = new();
    public SizeRows McpGroups { get; } = new();
    public long UsageInput { get; set; }
    public long UsageCacheCreate { get; set; }
    public long UsageCacheRead { get; set; }
    public long UsageOutput { get; set; }
    public double CacheHitRate { get; set; }
    public long MedianFinalContext { get; set; }

    /// <summary>Context-token-turns as recorded, and as the <c>archive</c> policy would leave them.</summary>
    public long CttTotal { get; set; }
    public long CttArchive { get; set; }
    public long ArchiveCandidates { get; set; }
    public SortedDictionary<string, ApiRow> Api { get; } = new(StringComparer.Ordinal);

    /// <summary>The counters per proxy lane; empty while every request ran on the agent lane.</summary>
    public SortedDictionary<string, ApiRow> Lanes { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, ApiRow> LaneTiers { get; } = new(StringComparer.Ordinal);

    /// <summary>Only with <c>--price</c>, so the default report is the same with or without a price table.</summary>
    public CostReport? Cost { get; set; }
    public EditRow Edits { get; } = new();
    public ReadDeltaRow ReadDelta { get; } = new();
    public ReadWholeRow ReadWhole { get; } = new();
    public ReadDenyRow ReadDeny { get; } = new();
    public RepeatRow Repeat { get; } = new();
    public ExpandAfterRow ExpandAfter { get; } = new();
    public BashUnmarkedRow BashUnmarked { get; } = new();
    public ThinkingRow Thinking { get; } = new();
    public ImageRow Images { get; } = new();
    public DictionaryRow Dictionary { get; } = new();

    /// <summary>Injected skill bodies per skill; null when no session had one.</summary>
    public SortedDictionary<string, SkillRow>? Skills { get; set; }
    public Subagents? Subagents { get; set; }
    public RepeatReadsRow RepeatReads { get; } = new();

    /// <summary>
    /// What the model re-types to edit: <c>old_string</c> of <c>Edit</c> and every
    /// <c>MultiEdit.edits[]</c>; <see cref="ToolInputBytes"/> is every tool input, serialized.
    /// </summary>
    internal sealed class EditRow
    {
        public long Calls { get; set; }
        public long OldBytes { get; set; }
        public long NewBytes { get; set; }
        public long ToolInputBytes { get; set; }
    }

    /// <summary>Re-reads of a path edited since its last read: what a diff could shorten.</summary>
    internal sealed class ReadDeltaRow
    {
        public long Calls { get; set; }
        public long Bytes { get; set; }
        public long ReadBytes { get; set; }
    }

    /// <summary>
    /// Whole-file native Reads, at or above the deny threshold, of a file <c>outline</c> has a
    /// grammar for. <see cref="Edited"/> of them were followed by an edit of the path within the
    /// guard window and needed the body.
    /// </summary>
    internal sealed class ReadWholeRow
    {
        public long Calls { get; set; }
        public long Bytes { get; set; }
        public long Edited { get; set; }
        public long EditedBytes { get; set; }
        public long ReadBytes { get; set; }
        public long ResultBytes { get; set; }
    }

    /// <summary>Native Reads the read hook denied, and what the next calls did.</summary>
    internal sealed class ReadDenyRow
    {
        public long Calls { get; set; }
        public long Tokens { get; set; }
        public long ThenMcp { get; set; }
        public long ThenNative { get; set; }
        public long ThenToolSearch { get; set; }
    }

    /// <summary>Tool results whose SHA-256 matches an earlier one in the session.</summary>
    internal sealed class RepeatRow
    {
        public long Calls { get; set; }
        public long Bytes { get; set; }
        public long ResultBytes { get; set; }
    }

    /// <summary>One class's calls and bytes in <see cref="RepeatReadsRow"/>.</summary>
    internal sealed class ClassCounts
    {
        public long Calls { get; set; }
        public long Bytes { get; set; }
    }

    /// <summary>
    /// Repeat native Reads of a path read before in the session, by why dedup and delta missed
    /// them: <c>fired</c> (an rtok marker), <c>changed</c> (an edit or a writing Bash since),
    /// <c>ranged</c> (other offset/limit), <c>subagent</c> (another agent read it),
    /// <c>hook_absent</c> (no rtok marker in the whole session), else <c>declined</c>.
    /// </summary>
    internal sealed class RepeatReadsRow
    {
        public ClassCounts Fired { get; } = new();
        public ClassCounts Changed { get; } = new();
        public ClassCounts Ranged { get; } = new();
        public ClassCounts Subagent { get; } = new();
        public ClassCounts HookAbsent { get; } = new();
        public ClassCounts Declined { get; } = new();

        public IEnumerable<(string Name, ClassCounts Counts)> Classes =>
            [
                ("fired", Fired),
                ("changed", Changed),
                ("ranged", Ranged),
                ("subagent", Subagent),
                ("hook_absent", HookAbsent),
                ("declined", Declined),
            ];

        public bool IsEmpty => Classes.All(c => c.Counts.Calls == 0);
    }

    /// <summary>Expands of an id the session was shown: what a cut made the agent fetch again.</summary>
    internal sealed class ExpandAfterRow
    {
        public long Calls { get; set; }
        public long Bytes { get; set; }
        public long ShownBytes { get; set; }
    }

    /// <summary>
    /// <c>cmd</c> measurements without an rtok marker: below the size gate (<c>raw</c>) or
    /// through a rule that shrank nothing (<c>unmatched</c>).
    /// </summary>
    internal sealed class BashUnmarkedRow
    {
        public long GateCalls { get; set; }
        public long GateBytes { get; set; }
        public long UnmatchedCalls { get; set; }
        public long UnmatchedBytes { get; set; }
    }

    /// <summary>Assistant <c>thinking</c> and <c>redacted_thinking</c> blocks.</summary>
    internal sealed class ThinkingRow
    {
        public long Blocks { get; set; }
        public long Bytes { get; set; }
    }

    /// <summary>
    /// <c>image</c> blocks. <see cref="Resident"/> multiplies each block's tokens by the requests
    /// at or after its turn; <see cref="NoSize"/> blocks are not PNG or JPEG and add no tokens.
    /// </summary>
    internal sealed class ImageRow
    {
        public long Blocks { get; set; }
        public long Bytes { get; set; }
        public long Tokens { get; set; }
        public long Resident { get; set; }
        public long NoSize { get; set; }

        /// <summary>By tool name; <c>user</c> for an image pasted into a prompt.</summary>
        public SortedDictionary<string, ImageSource> BySource { get; } =
            new(StringComparer.Ordinal);
    }

    internal sealed class ImageSource
    {
        public long Blocks { get; set; }
        public long Bytes { get; set; }
        public long Tokens { get; set; }
    }

    /// <summary>
    /// One skill's injected bodies. <see cref="Resident"/> is body bytes times the session's
    /// requests after the injection: what the context carried.
    /// </summary>
    internal sealed class SkillRow
    {
        public long Count { get; set; }
        public long Bytes { get; set; }
        public long Mean { get; set; }
        public long P95 { get; set; }
        public long Max { get; set; }
        public long EstTokens { get; set; }
        public long Resident { get; set; }
    }

    /// <summary>One bucket's counters and its prompt-cache hit rate.</summary>
    internal sealed record ApiRow(long Input, long CacheCreate, long CacheRead, long Output)
    {
        /// <summary>Cache reads over every input token billed (uncached, written and read).</summary>
        public double Hit =>
            SizeRows.Sat(SizeRows.Sat(CacheRead, CacheCreate), Input) is var d and > 0
                ? (double)CacheRead / d
                : 0;
    }

    /// <summary>One model's USD; null cost and saving for a model without a price row.</summary>
    internal sealed record CostRow(
        long Input,
        long CacheCreate,
        long CacheRead,
        long Output,
        double? Cost,
        double? Saved
    );

    /// <summary>
    /// USD over the store's <c>usage</c> rows. A model without a <c>[stats.prices]</c> row is
    /// named in <see cref="Unknown"/> and priced nowhere, never by guess.
    /// </summary>
    internal sealed class CostReport
    {
        public SortedDictionary<string, CostRow> Models { get; } = new(StringComparer.Ordinal);
        public List<string> Unknown { get; } = [];
        public double TotalCost { get; set; }
        public double TotalSaved { get; set; }

        /// <summary>USD per proxy lane; empty while every request ran on the agent lane.</summary>
        public SortedDictionary<string, LaneCost> Lanes { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>One lane's USD; <see cref="Partial"/> when a model on it has no price, so the cost is a floor.</summary>
    internal sealed record LaneCost(double Cost, bool Partial);
}
