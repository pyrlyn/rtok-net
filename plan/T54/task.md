---
status: todo
priority: P2
complexity: 3
readiness: 0%
agent:
---

# T54. Static SQLite in `rtok`

T8 ships `libe_sqlite3` beside the binary, from the `bundle_e_sqlite3` that
Microsoft.EntityFrameworkCore.Sqlite brings (README N8, the creator's choice). One binary with
SQLite linked in is the goal, but T4 found the sources uneven (research.md F):
- StaticLink.e_sqlite3 2.1.10.1 links on macOS and Linux, and ships Windows `.lib` files, but
  its prebuilt archives have unverified provenance.
- SourceGear.sqlite3 3.53.3 ships a static `libe_sqlite3.a` for Linux only (with SQLitePCLRaw 3),
  and only a dylib on macOS.

Done when:
- StaticLink.e_sqlite3 and SourceGear.sqlite3 are compared, together with building the
  amalgamation (with FTS5) in CI; one is chosen per OS, with the creator.
- The provenance of every prebuilt archive used is checked: the SQLite version and compile
  options it reports, against a build from the published amalgamation.
- For each OS (macOS, Linux, Windows), `research.md` F records whether it links, the binary's
  size, and the store's open and write latency, static against the dylib beside the binary.
  T4's tables there are the baseline.
- `StoreHost.targets` links the chosen source; the store tests and the AOT lane stay green, and
  FTS5 works.
- README N8 records the result.

Check: `rtok` on each OS runs the AOT store tests with no `libe_sqlite3` beside it, and
`research.md` F has the per-OS link, size and latency rows.
