namespace Rtok.Config;

/// <summary>the code graph and its tools</summary>
/// <remarks>Read from [plugins.graph].</remarks>
public sealed class GraphSection
{
    /// <summary>the code graph and its tools</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>per response; beyond it: head + "N more, expand &lt;id&gt;"</summary>
    public uint MaxTokens { get; set; } = 2000;

    /// <summary>impact: files grouped and ranked up to this budget, then "+K files, M refs not shown"; --all or 0 = every row</summary>
    public uint ImpactTokens { get; set; } = 1500;

    /// <summary>SessionStart repo map cap (counts toward the injection budget next to memory.recall_tokens); 0 = off</summary>
    public uint MapTokens { get; set; }

    /// <summary>SessionStart map order: refs = references per name; pagerank = files by personalized PageRank, personalized by recently edited files and the last checkpoint after a compact</summary>
    public string MapRank { get; set; } = "refs";

    /// <summary>symbol(): source lines shown per definition</summary>
    public uint BodyLines { get; set; } = 40;

    /// <summary>every symbol/callers/impact call walks the tree; false = index once, then `rtok graph index` or the watcher</summary>
    public bool AutoIndex { get; set; } = true;

    /// <summary>index a project the first time a session opens in it</summary>
    public bool AutoAddProjects { get; set; } = true;

    /// <summary>tags | lsp | auto | text: tags = tree-sitter index (default); lsp = language server from PATH, tags when it cannot answer; auto = per project and language, server first, tags second, text search when no grammar parses the project; text = in-process text search only (no call graph, no dead)</summary>
    public string Backend { get; set; } = "tags";

    /// <summary>how long one language-server wait (starting, still indexing) may take before the request falls back to tags</summary>
    public uint LspTimeoutMs { get; set; } = 40000;

    /// <summary>backend for one language, e.g. { go = "tags", rust = "lsp" }</summary>
    public IDictionary<string, string> BackendByLanguage { get; set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>off | notify: background re-index inside `rtok mcp`</summary>
    public string Watch { get; set; } = "off";

    /// <summary>follow references in manifests (Cargo path, npm file:/link:, go replace, Python path, submodules) into other directories, register and auto-link them</summary>
    public bool AutoLinkReferences { get; set; } = true;

    /// <summary>reference levels followed from the project (A -&gt; B is 1); reaching it is shown and logged</summary>
    public uint ReferenceDepth { get; set; } = 3;

    /// <summary>most projects references may add to the registry; reaching it is shown and logged</summary>
    public uint MaxAutoProjects { get; set; } = 20;

    /// <summary>alert when a project in the scope is missing, unreachable, has its backend down or a broken manifest link; shown by rtok doctor, graph projects and graph answers</summary>
    public bool Alerts { get; set; } = true;

    /// <summary>seconds between health checks in rtok mcp and rtok web (an alert needs two in a row); 0 = off</summary>
    public uint HealthCheckIntervalS { get; set; } = 60;

    /// <summary>seconds a node stays warm on the live graph canvas after a call (rtok web, rtok tui)</summary>
    public uint LiveHeatWindowS { get; set; } = 300;

    /// <summary>most call events the live panel lists per second; the counters still count every call</summary>
    public uint LiveMaxEventsPerS { get; set; } = 50;

    /// <summary>finished calls the live feed keeps</summary>
    public uint LiveFeedRows { get; set; } = 200;

    /// <summary>gitignore-style paths dropped from the walk</summary>
    public IList<string> Exclude { get; set; } = [];

    /// <summary>gitignore-style paths forced back in</summary>
    public IList<string> Include { get; set; } = [];

    /// <summary>ext = "grammar" remap for custom suffixes</summary>
    public IDictionary<string, string> Extensions { get; set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
