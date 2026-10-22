# Test fixtures

Shared inputs for every test lane (`RepoPaths.Fixture(...)` in `tests/Rtok.Testing`).

- `hooks/`: host hook payloads (Claude Code shape) read from stdin by `rtok hook` and
  `rtok-hook`. The latency harness (`tests/Rtok.Latency`) times the hook on each of them.
- `graph_truth.json`: labelled definitions and references of this repository's own C#, which
  `tests/Rtok.Plugins.Graph.Tests` scores the graph index against (rtok's `graph_truth.toml`).
