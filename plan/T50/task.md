---
status: todo
priority: P2
complexity: 2
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

Check: `research.md` has a dated row for each bench, with the .NET result next to rtok's.
