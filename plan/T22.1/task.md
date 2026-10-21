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

Check: A fake-server test shows extraction within the cost cap and no call when the extractor is off.
