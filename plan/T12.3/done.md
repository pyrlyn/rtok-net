# T12.3 done

`packages/Pyrlyn.Jsonc`: `JsoncDocument`, an immutable JSONC document edited by byte splices.

- Parsing is `Utf8JsonReader` with comments skipped and trailing commas allowed, so JSON5
  (`'a'`, `.5`, `NaN`, `\x41`) stays rejected. It builds a span tree of each entry's key,
  value and comma offsets. A UTF-8 BOM is kept. A failure is a `JsoncException` reading
  `file:line:column: reason`.
- `Get` (a `JsonElement`), `GetRaw` (the exact text), `Contains`, by key path of keys and array
  indices (`JsoncSegment`). With duplicate keys the last one wins, as in JSON.
- `Set` replaces only the value bytes. Otherwise it inserts after the last entry, following
  that entry's indentation, line break (LF or CRLF), indent unit (spaces or tab) and comma
  style. A comment ending the last entry's line stays with that entry. An empty container opens
  onto lines when its key starts a line. Missing objects on the way are created; index `Count`
  appends to an array; a document of only comments gets a root after them.
  An equal value (`JsonElement.DeepEquals`) returns the same instance.
- `Remove` takes the entry with one comma. It takes the entry's own lines when nothing else is
  on them; otherwise it takes only the entry's bytes, so a comment between the value and its
  comma, or after the comma, stays (rtok T627 cases). Every duplicate of a key goes.
- Every edit is parsed again before it is returned; a broken result throws instead.

Checked:
- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` and
  `dotnet csharpier check .` are green.
- The 37 tests include rtok's jsonc examples:
  - JSON5 rejection;
  - replacing a value keeps its trailing comment;
  - remove keeps foreign members and comments;
  - escaped keys and non-ASCII text;
  - the T627 comment/comma removals;
  - push/pull round trips that give back every byte.
- Three CsCheck property tests (2,000 samples each, run once at 200,000) cover remove, replace
  and insert on generated documents. The documents vary inline vs lines, trailing commas, block
  and line comments, 2/4/tab indents, LF/CRLF and nesting. Each test checks:
  - the result holds the expected value;
  - the bytes outside the edited entry's gap are unchanged;
  - every comment outside a removed or replaced value is still there.

CsCheck 4.9.1 is added to `Directory.Packages.props` for the property tests (N11).
