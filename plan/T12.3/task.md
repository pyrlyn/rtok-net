---
status: todo
priority: P1
complexity: 4
readiness: 0%
agent:
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
