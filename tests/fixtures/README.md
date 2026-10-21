# Test fixtures

Shared inputs for every test lane (`RepoPaths.Fixture(...)` in `tests/Rtok.Testing`).

- `hooks/`: host hook payloads (Claude Code shape) read from stdin by `rtok hook` and
  `rtok-hook`. The latency harness (`tests/Rtok.Latency`) times the hook on each of them.
