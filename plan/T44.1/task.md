---
status: todo
priority: P3
complexity: 1
readiness: 0%
agent:
---

# T44.1. Re-shoot dashboard screenshots

The screenshots and the brand pack's web-admin entry must show the Blazor dashboard (T44.2).

Done when `web/screenshots/` is regenerated and the brand README records the adoption.

Ports: rtok open T414.7 ([open work](../../docs/port/open-work.md)).

Tests:
- `Rtok.Web.Tests` unit: every image in `web/screenshots/` is referenced from a README, and every screenshot a README references exists.
- `Rtok.Web.Tests` unit: the brand README's web-admin entry is marked adopted and points at `web/screenshots/`.

Check: The screenshots in `web/screenshots/` are dated after the .NET dashboard went live.
