---
status: in progress
priority: P1
complexity: 4
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T12.3. Package Pyrlyn.Jsonc: lossless JSONC editing

rtok edits other agents' JSONC configs, and every byte outside its own entry must stay as it was.

Done when the package can do all of the following:
- read JSONC (comments, trailing commas);
- address a key path;
- insert, replace or remove one entry, keeping comments, whitespace and the commas around it;
- re-parse the result before returning it;
- refuse a file it cannot parse, with file:line.

Spec: catalog B3, catalog B18 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: no rtok task; new for .NET.

Check: A CsCheck property test edits random JSONC documents and shows that the bytes outside the edited entry are unchanged and the result re-parses.

Plan:
1. `eng/new-package.sh Pyrlyn.Jsonc`. Parse with `Utf8JsonReader` (comments skipped, trailing commas allowed) into a span tree: each member's key, value and comma offsets.
2. `JsoncDocument`: `Parse`/`Load` (a `JsoncException` names file:line:column), `Get`/`GetRaw`/`Contains` by key path (keys and array indices), `Set` (replace or insert, creating missing objects), `Remove`.
3. Edits are byte splices: replace only the value; insert after the last member in its indentation, newline and comma style; remove the entry, its comma and its own lines only. Every result is re-parsed before it is returned; an equal value returns the document unchanged.
4. Tests: examples from rtok's jsonc tests plus a CsCheck property test over generated JSONC (comments, trailing commas, nesting, CRLF): bytes outside the edited entry are unchanged and the result re-parses with the expected value.
