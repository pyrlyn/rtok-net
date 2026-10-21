---
status: todo
priority: P3
complexity: 3
readiness: 0%
agent:
---

# T22.1. Provider-backed memory extractor

rtok planned a built-in extractor beside the user-program one, blocked on the creator's decisions about provider, model, key storage and cost cap.

Done when, after the creator decides:
- an off-by-default extractor exists with a per-run cost cap;
- it is tested against a fake server.

Ports: rtok open T666.2 ([open work](../../docs/port/open-work.md)).

Tests:
- `Rtok.Config.Tests` unit: the extractor keys (provider, model, cost cap) validate, and the extractor defaults to off.
- `Rtok.Plugins.Memory.Tests` WireMock: with the extractor off, a session ends with no request to the fake provider.
- `Rtok.Plugins.Memory.Tests` WireMock: with the extractor on, the fake provider's reply is saved as notes through the same path as the user-program extractor.
- `Rtok.Plugins.Memory.Tests` WireMock: a run whose next request would pass the per-run cost cap stops before sending it, and the recorded spend stays within the cap.
- `Rtok.Plugins.Memory.Tests` WireMock: a provider error, timeout or malformed reply leaves the notes unchanged and never fails the hook.
- `Rtok.Plugins.Memory.Tests` unit: the API key comes from the chosen key storage and never appears in logs, the store or exported notes.

Check: A fake-server test shows extraction within the cost cap and no call when the extractor is off.
