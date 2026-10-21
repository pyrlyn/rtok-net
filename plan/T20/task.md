---
status: todo
priority: P2
complexity: 3
readiness: 0%
agent:
---

# T20. guard plugin: deny repeated read-only calls

Agents re-run the same read-only command or Read; the guard points them at the archive instead.

Done when:
- A duplicate Read or Bash within `window_turns` is denied with the archive id.
- Keys are normalized as rtok does: cwd, cd hops, quoting, the `rtok run` wrapper stripped.
- Read-only command classes are flag-aware, and writers clear the keys.
- The optional Grep/Glob redirect and the skill digest behave as in the catalog.
- The deny path reads only archive metadata.

Tests:
- `Rtok.Plugins.Guard.Tests` unit: a second identical Read or Bash within `window_turns` (8) is denied with a reason naming `rtok expand <id>`, and the same call after the window is allowed.
- `Rtok.Plugins.Guard.Tests` unit: a repeat with no archive id is allowed, and a body over 256 KiB is not archived and clears its key.
- `Rtok.Plugins.Guard.Tests` unit: a key-normalization table collapses whitespace only outside quotes and escapes, turns newlines into `;`, strips the `rtok run` wrapper, keeps the last cd target, never keys a relative cd hop, and gives no key for an unterminated quote.
- `Rtok.Plugins.Guard.Tests` unit: the Read key is `read\t{path}[slice]` with offset/limit/pages as a suffix, a relative path resolves lexically against the hook cwd, and no cwd or a `..` gives no key.
- `Rtok.Plugins.Guard.Tests` unit: a table marks every rtok read-only class read-only (ls, cat, head, tail, grep, rg, find, tree, wc, git status/log/diff/show, git branch only with listing flags) across pipes and redirects.
- `Rtok.Plugins.Guard.Tests` unit: a table marks every rtok writer as a writer: `$(` and backticks, awk writers, sed `w`/`e`/`r`/`-f`/`-i` and `[` in a regex, find `-fprint*`/`-fls`/`-ok*`, and git `--output` with its abbreviations.
- `Rtok.Plugins.Guard.Tests` unit: a mutating Bash clears all bash and read keys, an Edit/Write clears the bash keys plus its own read key, and an unkeyable Edit/Write clears every read key.
- `Rtok.Plugins.Guard.Tests` CsCheck: key normalization and classification never throw on arbitrary non-ASCII input and slice only on char boundaries.
- `Rtok.Plugins.Guard.Tests` unit: the deny path reads only archive size metadata (an NSubstitute archive whose body read fails the test) and writes a Measurement with before = body bytes, est = bytes/4 and after = 0.
- `Rtok.Plugins.Guard.Tests` unit: with `deny_grep_glob=true` Grep is sent to `search` and Glob to `tree` only while the read plugin is enabled, and a symbol-shaped Grep gets the graph definition.
- `Rtok.Plugins.Guard.Tests` unit: with `skills=true` a Skill body over `skill_max_bytes` (8192) resolves to its SKILL.md (project `.claude/skills` before the plugin install path) and is digested, quoted YAML frontmatter keys count, and unparseable frontmatter is exempt.
- `Rtok.Plugins.Guard.Tests` integration: in a temp home with a real store, `rtok hook PreToolUse` denies a repeated Read and allows on garbage stdin.
- `Rtok.Benchmarks` bench: the deny check costs the same with a 200 KiB archived body as with a 1 KiB one.

Spec: catalog A8, catalog B13 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T2.6, T50.4, T55.8, T55.9, T55.16, T57.1, T62.1, T299, T322, T350, T369, T445, T450, T555, T556, T557, T558, T559.

Fuzz: `guard-key` in `tests/Rtok.Fuzz`: key normalization over any command or Read argument is deterministic and idempotent.

Check: Table tests cover the key normalization and every read-only and writer class from rtok, plus non-ASCII input.
