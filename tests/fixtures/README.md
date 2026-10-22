# Test fixtures

Shared inputs for every test lane (`RepoPaths.Fixture(...)` in `tests/Rtok.Testing`).

- `hooks/`: host hook payloads (Claude Code shape) read from stdin by `rtok hook` and
  `rtok-hook`. The latency harness (`tests/Rtok.Latency`) times the hook on each of them.
- `graph_truth.json`: labelled definitions and references of this repository's own C#, which
  `tests/Rtok.Plugins.Graph.Tests` scores the graph index against (rtok's `graph_truth.toml`).
- `stats/`: one Claude Code session with a sub-agent (`claude/proj/sess-1/subagents/`) whose
  results cover every `rtok stats` row: tools, Bash families, MCP groups, repeat reads, an
  image, thinking, an edit and repeated paths and identifiers. The `rtok stats` goldens in
  `tests/Rtok.Cli.Tests` run on it.
- `tokens/o200k.json`: strings with their o200k token counts, checked against
  Microsoft.ML.Tokenizers in `tests/Rtok.Measure.Tests` (the Release binary has no vocabulary).
