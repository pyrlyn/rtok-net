---
status: todo
priority: P1
complexity: 2
readiness: 0%
agent:
---

# T10. Logging: rotating log and `rtok logs`

One bounded log for everything (rtok D26), cheap enough for the hook path.

Done when:
- `[log]` keys behave as in the catalog: path, max_bytes 1 MiB, 5 files, level, to_db. The level floor is checked before any IO, and an `errors.log` copy is kept. Pyrlyn.RotatingLog (T12.10) does the writing.
- A single funnel writes both the file line and the logs row, and fails open.
- `rtok logs [--lines N]`, `logs export` and `logs watch` work. `watch` follows rotation by file identity and restarts after in-place truncation.
- `RTOK_LOG` debug output goes to stderr.

Spec: catalog A27 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T24.0, T24.1, T24.2, T24.3, T24.4, T24.5, T225, T225.1, T270, T589.

Check: Rotation, retention and follow tests pass, including truncation and an empty replacement file.
