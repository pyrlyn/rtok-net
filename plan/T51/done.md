# T51 done

Three decisions are recorded as N19 in `plan/README.md`. The creator approved each recommendation. Nothing is ported, so there is no port test.

## Evidence

The evidence comes from the Rust rtok at `ef091589`. rtok's closed tasks are in its `done.md:5001-5105`, with follow-ups T454 and T668.

### Embeddings beside FTS5 (T29.0-T29.2): deferred

The code and how it is gated:
- Brute-force cosine over BLOBs, merged with FTS5 by RRF.
- About 740 lines in `crates/rtok-store/src/embed.rs` and 340 in `src/store/http_embed.rs`.
- It is gated at runtime only, by `plugins.memory.embed.enabled=false`.
- The default `local` provider uses `hash_embed`: lexical feature hashes, not a model.

What the bench shows:
- On rtok's own bench, FTS5 and hybrid both score 20/20 at N=1, 10, 30 and 100 (rtok `research.md:1264`).
- T668 records "no data" for the `http` provider.
- The only Measurement row is a 1/0 check that the fixture note was found.

What else touches it:
- Graph search does not use embeddings.
- The proxy semantic cache reuses `hash_embed` under its own `embed_backend` key. That key is off by default and measured 0 hits; it belongs to the proxy task, not this one.

### WASM plugin host (T32.0-T32.2): dropped

What it was:
- A wasmi host of about 480 lines plus a 138-line no_std demo guest.
- It sat behind the Cargo feature `wasm-host`, which is not in the default features. No CI, justfile or dist config enabled it, so no rtok release shipped it.

Why it is dropped:
- No benefit is measured beyond the demo guest's own Measurement row.
- The docs are stale.
- With the feature on, the hook path would have scanned the plugin directory, which contradicts its own plan.
- It is dormant upstream since T32.2.
- In .NET, N12 already covers out-of-tree plugins, and WACS carries trim warnings (`research.md`).

### Tiered L0/L1/L2 archive context (T33.0-T33.2): deferred

What it is:
- About 60 lines in rtok's `src/plugins/archive/mod.rs`, behind `plugins.archive.tiers=false`.
- It is lossless: L2 is the archive plus `expand`.
- Inject does not branch on it.

What the bench shows:
- One synthetic gate, P33 CTT on a 12-result fixture: 126872 to 109908 tokens (86.6%).
- No real-session measurement.
- rtok itself keeps P33 "later until a bench beats the lossless archive".

## Checked

`plan/README.md` has one decision line per experiment, under N19.

Gates:
- `dotnet build rtok.slnx -c Release -warnaserror`
- `dotnet test`
- `dotnet csharpier check .`

They ran on the branch before the merge. The change is documentation only.

Commit: `T51: Experimental extension points: decide and gate`.
