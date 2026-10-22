---
status: todo
priority: P2
complexity: 4
readiness: 0%
agent:
---

# T50. Parity benches on the .NET build

rtok's savings claims rest on measured benches; the .NET build must reproduce them before it replaces rtok. Several runs cost API money and need the creator.

Done when dated rows are recorded in `research.md` for:
- the replay and proxy bench floors (76 %, 86.4 %);
- `tools_rewrite` saving over 20+ sessions (rtok T124);
- the spawn brief on and off (T131);
- grep_symbol follow-up rate (T369.1);
- the defaults bench (T385.3);
- the compressor gate (T385.9.3);
- the paid live benches (T394).

Ports: rtok open T124, T369.1, T385.3, T385.9.3, T394 ([open work](../../docs/port/open-work.md)).

Tests:
- `Rtok.Measure.Tests` integration: the replay bench on the shared fixture saves at least 76 %.
- `Rtok.Proxy.Tests` integration: the proxy bench on the proxy fixture saves at least 86.4 % end to end.
- `Rtok.Measure.Tests` unit: the `tools_rewrite` saving per request is computed from Measurement rows over a fixture of 20 recorded sessions.
- `Rtok.Measure.Tests` unit: the grep_symbol follow-up rate counts Grep and Read calls within 3 calls after a grep_symbol answer on a fixture transcript.
- `Rtok.Measure.Tests` integration: `rtok bench` runs the spawn brief and each defaults setting (compress, context editing, skills, live_blobs) on and off against a fake agent script and prints cost per passed task.
- `Rtok.Measure.Tests` integration: the compressor gate bench on the must-keep fixture counts compressor tokens, keeps every must-keep item and expands every compressed block back to its original.
- `Rtok.Measure.Tests` integration: the paid live bench configs from rtok T394 (nudges A/B, plain vs rtok, graph MCP vs native) run end to end against a fake agent script, so the paid run needs no code change.

Check: `research.md` has a dated row for each bench, with the .NET result next to rtok's.
