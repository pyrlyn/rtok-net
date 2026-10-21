namespace Rtok.Config;

/// <summary>optional vector search beside FTS5</summary>
/// <remarks>Read from [plugins.memory.embed].</remarks>
public sealed class MemoryEmbedSection
{
    /// <summary>FTS5-only when false; vector search is opt-in</summary>
    public bool Enabled { get; set; }

    /// <summary>"local" = offline hash vectors | "http" = an OpenAI-compatible embeddings API</summary>
    public string Provider { get; set; } = "local";

    /// <summary>with "http": the endpoint's model name</summary>
    public string Model { get; set; } = "all-MiniLM-L6-v2";

    /// <summary>with "http": must equal the endpoint's vector length</summary>
    public uint Dimensions { get; set; } = 384;

    /// <summary>when enabled: RRF(fts5, knn) for mem_search and prompt_recall; the hook reads stored vectors only; false = knn only</summary>
    public bool Hybrid { get; set; } = true;

    /// <summary>"http": base URL, e.g. "http://localhost:11434/v1" (Ollama) or "https://api.openai.com/v1"; note text is sent there</summary>
    public string Url { get; set; } = "";

    /// <summary>"http": name of the env var holding the key; empty = no Authorization header</summary>
    public string ApiKeyEnv { get; set; } = "";

    /// <summary>"http": a longer text is never sent (the note search runs instead)</summary>
    public uint MaxChars { get; set; } = 8000;
}
