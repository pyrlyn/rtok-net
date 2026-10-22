---
status: todo
priority: P1
complexity: 2
readiness: 0%
agent:
---

# T18.1. cmd formatter: `cat` and `sed -n` of a source file as its outline

Split from T18: rtok's `catfile.rs` (rtok T674) prints a long source file as its definitions and reads a file again as a pointer or a diff. It needs the tags outline (T24) and the read plugin's re-read dedup (T19, on the T9 archive), which T18 did not have. Waits on T19 and T24. The cmd plugin calls their outline and dedup; it keeps no copy (one logic, one place).

Done when:
- A bare `cat <file>` of a source file over 80 lines prints each definition's signature with its `start-end` span instead of a head/tail cut; a short file, a config or markup file and any other argv go to the `cat` rule.
- `sed -n a,bp <file>` prints the asked lines as they are.
- A file read again in the same session prints a pointer when it is unchanged and a unified diff when it changed, from the dedup `rtok read` uses.
- The `cat` arm is in `FamilyFormatters.Arms` with a golden, so T18's coverage test holds.

Tests:
- `Rtok.Plugins.Cmd.Tests` unit: cat of a source file over 80 lines prints definition spans and signatures, and a repeat in the same session prints a pointer or a unified diff.
- `Rtok.Plugins.Cmd.Tests` Verify: a `cat` golden from rtok's `cmd_golden` meets its `min_saving` floor.

Spec: catalog A4 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T674.

Check: `cat` of a long source file prints its outline, and a re-read in the same session prints a pointer or a diff.
