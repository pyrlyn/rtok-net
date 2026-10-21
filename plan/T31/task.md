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

Tests:
- `Rtok.Report.Tests` Verify: `rtok report --format md` on the fixture store, with the window, cache, expand, config, doctor and recommendations sections and their evidence row counts.
- `Rtok.Report.Tests` Verify: `rtok report --format html` on the fixture store.
- `Rtok.Report.Tests` unit: the html has inline CSS and SVG bars, stable ids and no `http:`, `https:` or external `src`/`href`, and its bars share one scale.
- `Rtok.Report.Tests` unit: the sections are built once into the document model, and the md, html and pdf `IReportRenderer`s each render that same instance.
- `Rtok.Report.Tests` unit: each advice rule (expand rate > 0.05, net saving ≤ 0, cache busts, a hook with ≥ 10 calls and no Measurement, inject over budget, archive keep_turns vs re-reads, idle hooks) fires on its own fixture, findings are ordered by recoverable tokens, and an empty store prints "No recommendations.".
- `Rtok.Report.Tests` unit: every saving figure in a section comes from Measurement rows, and a hook without one gets no saving line.
- `Rtok.Report.Tests` Verify: `rtok report --ai` on the fixture store, with toon tables, `#id` anchors, the units line and the task list `1. [rule] finding (evidence)`.
- `Rtok.Report.Tests` CsCheck: for any sections and `report.budget_tokens`, the whole serialized `--ai` text (header, store path, `dropped:` line, anchors) stays within the budget, keeps whole sections first-fit, names the rest on `dropped:`, and degrades to the title and dropped list, then to empty.
- `Rtok.Report.Tests` unit: an explicit `--format` beats `[report] format`, `--since` rejects `5x`, `-1d` and empty, and `--out` writes to the given path.
- `Rtok.Report.Tests` unit: non-finite values print `—`, u64 counts saturate when cast for charts, and top sinks are filtered to read, cmd and mcp before the top-10 cut.
- `Rtok.Report.Pdf.Tests` unit: the PDF of a fixture model is A4 with Helvetica only, a contents page with physical page numbers, one bookmark per section and UTF-8 text intact, and an orphan heading moves with its block.
- `Rtok.Report.Pdf.Tests` unit: the QuestPDF licence type is set explicitly before the first document is generated.
- `Rtok.Core.Tests` arch: only `Rtok.Report.Pdf` references QuestPDF, and none of its public types exposes a QuestPDF type.
- `Rtok.Smoke` smoke: the published `rtok report --format pdf --out <tmp>` on the fixture store writes a PDF that opens (a `%PDF-` header, a `%%EOF` trailer, at least one page).

Spec: catalog A17, catalog B22 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T22.0, T22.1, T22.2, T22.3, T22.4, T22.5, T22.6, T34.4, T36.13.

Check: Golden md, html and `--ai` outputs match on a fixture store, the `--ai` output never exceeds its budget, the AOT binary writes a PDF that opens, and the architecture test shows only `Rtok.Report.Pdf` references QuestPDF.
