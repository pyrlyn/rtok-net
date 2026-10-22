namespace Rtok.Measure;

/// <summary>
/// The <c>dictionary</c> row: how many bytes repeated long paths and identifiers take in the
/// messages a request carries, and what a legend (<c>~12</c> for <c>src/proxy/mod.rs</c>) could
/// save net. The transcripts stand in for request bodies: they are the history the agent re-sends
/// on every request.
/// </summary>
internal sealed class DictionaryRow
{
    /// <summary>Shorter paths cost less than their code plus legend line.</summary>
    private const int MinPath = 16;
    private const int MinIdent = 12;

    /// <summary>
    /// Requests sampled per session: every request re-sends the history, so a few evenly spaced
    /// ones show how the share moves as the context grows.
    /// </summary>
    private const int Checkpoints = 6;

    public long Requests { get; set; }

    /// <summary>Bytes of the tool inputs and results in the sampled requests.</summary>
    public long InputBytes { get; set; }

    /// <summary>
    /// What the provider billed as input for the same requests; the message content above leaves
    /// out the system prompt, tool schemas and thinking, so it is only a part of this.
    /// </summary>
    public long RequestTokens { get; set; }
    public Kind Paths { get; } = new();
    public Kind Idents { get; } = new();

    /// <summary>Nothing repeated: no row.</summary>
    public bool IsEmpty => Paths.Repeated + Idents.Repeated == 0;

    /// <summary>One kind of token.</summary>
    internal sealed class Kind
    {
        /// <summary>Bytes of every occurrence of a token seen at least twice: the ceiling.</summary>
        public long Repeated { get; set; }

        /// <summary>Bytes the cache-safe legend removes from tool results, net of the definitions.</summary>
        public long Saved { get; set; }
    }

    /// <summary>
    /// Adds the session's sampled requests. A request is the tool inputs and results up to its
    /// turn, counted afresh after a compaction.
    /// </summary>
    public void Fold(Transcript parsed)
    {
        var items = parsed
            .ToolResults.Select(r => (r.Turn, Text: r.Content, Rewritable: true))
            .Concat(parsed.ToolUses.Select(u => (u.Turn, Text: Raw(u), Rewritable: false)))
            .OrderBy(i => i.Turn)
            .ToList();
        var sizes = parsed
            .Usages.Select(u => (u.Turn, Tokens: u.Input + u.CacheCreate + u.CacheRead))
            .ToList();
        var at = new List<(int Turn, long Tokens)>();
        for (var k = 1; k <= Checkpoints; k++)
        {
            var i = sizes.Count * k / Checkpoints - 1;
            if (i >= 0 && (at.Count == 0 || at[^1].Turn != sizes[i].Turn))
            {
                at.Add(sizes[i]);
            }
        }
        var counts = new Counts();
        var compactions = parsed.Compactions;
        var (next, c) = (0, 0);
        foreach (var (turn, tokens) in at)
        {
            for (; next < items.Count && items[next].Turn <= turn; next++)
            {
                for (; c < compactions.Count && compactions[c] <= items[next].Turn; c++)
                {
                    counts = new Counts();
                }
                counts.Add(items[next].Text, items[next].Rewritable);
            }
            for (; c < compactions.Count && compactions[c] <= turn; c++)
            {
                counts = new Counts();
            }
            if (counts.Bytes > 0)
            {
                Merge(counts, tokens);
            }
        }
    }

    /// <summary>
    /// Calls <paramref name="f"/> with <c>(true, path)</c> or <c>(false, identifier)</c> for each
    /// candidate. A <c>/</c> inside a run makes it a path, and identifiers inside it are not
    /// counted again; <c>//host/x</c> is the tail of a URL. An identifier is snake_case or
    /// camelCase, so prose words and hex hashes stay out.
    /// </summary>
    internal static void Scan(string text, Action<bool, string> f)
    {
        var i = 0;
        while (i < text.Length)
        {
            if (!IsRun(text[i]))
            {
                i++;
                continue;
            }
            var start = i;
            while (i < text.Length && IsRun(text[i]))
            {
                i++;
            }
            var run = text[start..i].TrimEnd('.', '-');
            if (run.Contains('/', StringComparison.Ordinal))
            {
                if (
                    run.Length >= MinPath
                    && !run.StartsWith("//", StringComparison.Ordinal)
                    && run.Any(char.IsAsciiLetter)
                )
                {
                    f(true, run);
                }
                continue;
            }
            foreach (var part in run.Split(['.', '-', '~']))
            {
                if (IsIdent(part))
                {
                    f(false, part);
                }
            }
        }
    }

    private static bool IsRun(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '/' or '~' or '-';

    private static bool IsIdent(string s) =>
        s.Length >= MinIdent
        && (char.IsAsciiLetter(s[0]) || s[0] == '_')
        && !s.All(char.IsAsciiHexDigit)
        && (
            s.Contains('_', StringComparison.Ordinal)
            || (s.Any(char.IsAsciiLetterLower) && s.Any(char.IsAsciiLetterUpper))
        );

    private static string Raw(Transcript.ToolUse u) =>
        u.Input.ValueKind == System.Text.Json.JsonValueKind.Undefined
            ? "null"
            : u.Input.GetRawText();

    private void Merge(Counts counts, long tokens)
    {
        Requests++;
        InputBytes += counts.Bytes;
        RequestTokens += tokens;
        foreach (var (isPath, len, n) in counts.All.Seen(2))
        {
            (isPath ? Paths : Idents).Repeated += len * n;
        }
        var rank = 0;
        foreach (var (isPath, len, n) in counts.Results.Seen(3))
        {
            // `~` plus the decimal rank. A code is defined inline at the token's second use in
            // a result (`path{~7}`, code + 2 extra bytes), so the history before a message
            // never changes; only the third and later uses shrink.
            rank++;
            long code = 1 + rank.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
            var gain = (n - 2) * Math.Max(len - code, 0);
            (isPath ? Paths : Idents).Saved += Math.Max(gain - (code + 2), 0);
        }
    }

    /// <summary>
    /// One simulated request: <see cref="All"/> is everything it carries, <see cref="Results"/>
    /// only the tool results, the one place a proxy may rewrite.
    /// </summary>
    internal sealed class Counts
    {
        public Tally All { get; } = new();
        public Tally Results { get; } = new();
        public long Bytes { get; private set; }

        public void Add(string text, bool rewritable)
        {
            Bytes += Transcript.Utf8(text);
            All.Add(text);
            if (rewritable)
            {
                Results.Add(text);
            }
        }
    }

    /// <summary>Occurrences per token.</summary>
    internal sealed class Tally
    {
        private readonly Dictionary<string, long> _paths = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _idents = new(StringComparer.Ordinal);

        public void Add(string text) =>
            Scan(
                text,
                (isPath, token) =>
                {
                    var map = isPath ? _paths : _idents;
                    map[token] = map.GetValueOrDefault(token) + 1;
                }
            );

        /// <summary>
        /// Tokens seen at least <paramref name="min"/> times, biggest (length × count) first;
        /// ties by kind and token, so the ranking is the same on every run.
        /// </summary>
        public List<(bool IsPath, long Len, long N)> Seen(long min) =>
            [
                .. _paths
                    .Where(p => p.Value >= min)
                    .Select(p => (IsPath: true, Token: p.Key, N: p.Value))
                    .Concat(
                        _idents
                            .Where(p => p.Value >= min)
                            .Select(p => (IsPath: false, Token: p.Key, N: p.Value))
                    )
                    .OrderByDescending(t => t.Token.Length * t.N)
                    .ThenByDescending(t => t.IsPath)
                    .ThenBy(t => t.Token, StringComparer.Ordinal)
                    .Select(t => (t.IsPath, (long)t.Token.Length, t.N)),
            ];
    }
}
