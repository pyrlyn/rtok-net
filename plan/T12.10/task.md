---
status: todo
priority: P1
complexity: 2
readiness: 0%
agent:
---

# T12.10. Package Pyrlyn.RotatingLog: rotating file log for short processes

The hook path cannot afford a logging framework, but it still has to write log lines.

Done when:
- Appends are size-bounded, with `.N` shifting under a lock.
- A level floor is checked before any IO.
- A sibling error copy is kept.
- No DI and no host are needed.
- It is safe across processes.

Spec: catalog A27 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: no rtok task; new for .NET.

Check: A concurrency test with several writer processes loses no line and never leaves more than the configured file count.
