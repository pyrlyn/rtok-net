---
status: todo
priority: P2
complexity: 2
readiness: 0%
agent:
---

# T21. inject plugin, prompt modes and prose compression

Injected context must stay inside its budget and be byte-stable, so it never breaks the prompt cache (rtok D5, D7).

Done when:
- Injections are emitted by priority up to `inject_budget_tokens`, with `dropped:` lines and Measurements.
- Mode files (`modes/*.md`, copied from rtok) are read capped and are byte-stable.
- `compress_prose` keeps rtok's glue and code-fence rules.

Tests:
- `Rtok.Plugins.Inject.Tests` unit: injections from several plugins are emitted by priority up to `inject_budget_tokens` (800), the overflow gets `dropped:<plugin>:<est>` lines, and each emitted and dropped entry writes a Measurement with kind=inject.
- `Rtok.Plugins.Inject.Tests` unit: `setup --mode terse,yagni` makes `modes/terse.md` and `modes/yagni.md` inject at priority 5 once per session, and a later turn of the same session injects nothing.
- `Rtok.Plugins.Inject.Tests` unit: the shipped `modes/*.md` files copied from rtok are each at most 250 tokens.
- `Rtok.Plugins.Inject.Tests` unit: a mode file is read capped at (ceil(budget·rate)+1)·4 bytes, a partial UTF-8 tail is dropped, and fitting Unicode content survives in full.
- `Rtok.Plugins.Inject.Tests` CsCheck: for any mode file bytes the capped read is valid UTF-8 and a prefix of the decoded file.
- `Rtok.Plugins.Inject.Tests` unit: two runs over the same session and turn give byte-identical injected text.
- `Rtok.Plugins.Inject.Tests` unit: a missing or unreadable mode file injects nothing and never throws.
- `Rtok.Plugins.Inject.Tests` unit: a `compress_prose` table keeps glue on both sides of a word (`foo_a`, `/tmp/a`, `item1a`) and treats non-ASCII letters as glue.
- `Rtok.Plugins.Inject.Tests` unit: a fence table follows CommonMark: backtick and tilde fences, up to 3 spaces of indent, a closer at least as long as the opener, an unclosed fence running to the end, and a mid-line triple backtick staying inline.
- `Rtok.Plugins.Inject.Tests` CsCheck: `compress_prose` output is never longer than its input and every fenced block survives byte for byte.

Spec: catalog A7, catalog B16 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T2.4, T7.1, T188, T604, T606, T608.

Check: Repeated runs give byte-identical output, and the fence and glue cases from the catalog pass.
