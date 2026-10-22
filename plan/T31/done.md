# T31 done

`rtok report [--format md|html|pdf] [--out] [--since] [--ai] [--config]` turns the ledgers into sections and findings with evidence. The sections are built once into a plain document model, and md, html, pdf and `--ai` are `IReportRenderer`s over that one instance (README N18).

## What was done

- **Store (`Rtok.Store`):** windowed report reads, `RtokStore.Report.cs`:
  - `CallCounts`, `LedgerCounts`, `CallTimings` and `MeasurementsSince`.
  - `UsageSessions`, `UsageTurns` and `ArchiveDecisionCounts` take an optional `since`.
  - `CallTimings` is two queries. The EF precompiler cannot translate a subquery over the projected call.
  - Fix: `UsageSessions` and `UsageTurns` spread the `IQueryable` straight into a collection expression, with no terminal operator for EF to intercept. So `rtok stats --cache` threw "Query wasn't precompiled" on the NativeAOT binary, on `main` too. Both now end in `AsEnumerable()`.
- **`src/Rtok.Report`** (new):
  - The document model: sections; paragraphs of spans; tables; code blocks; bar charts; findings.
  - `ReportFacts` reads the store once for the window.
  - `ReportBuilder` builds the window, savings, latency, cache, expand, config, doctor and recommendations sections.
  - `Advice` holds the seven rules, ordered by recoverable tokens; an empty store gives "No recommendations.".
  - Renderers: `MarkdownRenderer`, `HtmlRenderer` and `AiRenderer`.
    - `HtmlRenderer`: inline CSS, SVG bars on one scale per chart, stable section ids.
    - `AiRenderer`: toon tables, `#id` anchors, the units line, a numbered task list, and first-fit sections within `report.budget_tokens` with a `dropped:` line.
  - `ReportPage`: flags over config (`--ai` wins over `--format`), `--since` validation and output to `--out` or stdout.
  - `CacheHealth` in `Rtok.Measure` is now public; the report shares its sessions with `stats --cache`.
- **`src/Rtok.Report.Pdf`** (new): `PdfRenderer` on QuestPDF.
  - A4, a contents page with physical page numbers, one bookmark per section, bars, and headings kept with their block.
  - The licence type is set in the static constructor.
  - No public type exposes QuestPDF.
- **CLI:** `rtok report` (`ReportCommand`); `report.format` is validated as md, html or pdf. It renders doctor without probes. Updated for it: `docs/cli/commands.json`, the help, completion and man goldens, and `JsonTests`.
- **Tests:** `ManualClock` moved to `Rtok.Testing`, shared by the store, model and web tests.

## Deviations

- **Font:** the PDF uses Lato, which QuestPDF ships, with system fonts off, not Helvetica. Helvetica would come from the machine's fonts, so the output would differ between machines. The test checks that the PDF uses one font family.
- **Self-contained HTML check:** the html test checks markup only (tags and attributes have no `http:`, `https:`, `src` or `href`). Config values such as URLs are legitimate text in the config section.
- **Config in goldens:** the goldens pin only the non-default config rows. The full table has some 420 default rows, and it would change with every new config key.
- **Window:** `--since` applies to every ledger: calls, measurements, usage and archive decisions.
- **Charts:** each chart has its own scale.
- **Idle hooks:** one rule covers idle hooks (≥ 10 calls, no Measurement).
- **Toon:** `--ai` uses a minimal toon table encoder; T28 owns the full one.
- **Lanes table:** skipped. There is no lane data in the store yet.

## How it was checked

- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test`, `dotnet csharpier check .` and `npx --yes markdownlint-cli2@0.23.3`.
- **`Rtok.Report.Tests`** (35 tests):
  - Verify goldens of md, html and `--ai` on the fixture store, which fires every rule.
  - Advice rules one by one, with ordering.
  - Savings come only from Measurement rows; a hook without one has no saving line.
  - Html checks: self-contained, ids, one scale, escaping. md, html and pdf render the same document instance.
  - CsCheck property over the `--ai` budget.
  - Flags, `--since` refusals and `--out`.
  - Formatting: `—` for non-finite values, saturation, the top-10 sinks cut.
- **`Rtok.Report.Pdf.Tests`** (7 tests, PdfPig reads the PDF back):
  - Licence set first; A4 in one font family.
  - Contents page with physical page numbers; one bookmark per section.
  - UTF-8 intact; a missing glyph does not stop the document.
  - The orphan heading moves with its block. A mutation check confirmed the test catches the regression.
- **`Rtok.Core.Tests` arch:** only `Rtok.Report.Pdf` references QuestPDF, and its exported API exposes no QuestPDF type. A mutation check confirmed it.
- **NativeAOT:**
  - `Rtok.Smoke` `Report_writes_a_pdf_that_opens`: the published `rtok report --format pdf --out` writes `%PDF-` … `%%EOF` with pages.
  - The AOT lane's store test runs every new report read.
  - `stats --cache` was run by hand on the published binary.
- **Fuzz:** a `report-html` target with 4 seeds. Any strings in every slot:
  - html opens only the renderer's own tags and has one section;
  - md keeps its two headings, three-pipe table rows and closed fences.

  `Rtok.Fuzz.Tests` passes (87 tests).
