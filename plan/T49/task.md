---
status: todo
priority: P1
complexity: 3
readiness: 0%
agent:
---

# T49. Security: threat model and audit findings

rtok's 2026-10-14 audit found eight issues (S1-S8). Each one goes into the matching task, and this task makes sure none is lost and writes the threat model (rtok I-S5).

Done when:
- `docs/security.md` describes the threat model per surface: hooks, stdio MCP, HTTP MCP, proxy, dashboard.
- Each finding has a test in its task:
  - S1, S8 in T15;
  - S2 in T44;
  - S3 in T8;
  - S4 in T19;
  - S5, S6 in T27 and T44;
  - S7 in T14.

Ports: rtok open S1, S2, S3, S4, S5, S6, S7, S8, I-S5 ([open work](../../docs/port/open-work.md)).

Check: `docs/security.md` exists and each of S1-S8 maps to a named passing test.
