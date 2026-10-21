---
status: todo
priority: P2
complexity: 3
readiness: 0%
agent:
---

# T31. Report: md, html, pdf and `--ai`

`rtok report` turns the ledgers into findings with evidence (rtok D24).

Done when:
- `rtok report [--format md|html|pdf] [--out] [--since] [--ai]` renders the sections and advice rules from the catalog.
- HTML is self-contained.
- Sections are built once as a plain document model. md, html and pdf are `IReportRenderer` implementations over it (README N18).
- PDF is rendered by `Rtok.Report.Pdf` on QuestPDF. It is the only project that references QuestPDF, with the licence type set explicitly.
- `--ai` keeps to its token budget.

Spec: catalog A17, catalog B22 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T22.0, T22.1, T22.2, T22.3, T22.4, T22.5, T22.6, T34.4, T36.13.

Check: Golden md, html and `--ai` outputs match on a fixture store, the `--ai` output never exceeds its budget, the AOT binary writes a PDF that opens, and the architecture test shows only `Rtok.Report.Pdf` references QuestPDF.
