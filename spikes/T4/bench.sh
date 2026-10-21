#!/usr/bin/env bash
# T4 latency spike: publish the probes AOT for this machine and time them with hyperfine.
# Usage: spikes/T4/bench.sh [runs]   (writes out/<rid>/results.md)
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
repo=$(cd "$here/../.." && pwd)
runs=${1:-200}

case "$(uname -s)-$(uname -m)" in
  Darwin-arm64) rid=osx-arm64 ;;
  Darwin-x86_64) rid=osx-x64 ;;
  Linux-aarch64) rid=linux-arm64 ;;
  Linux-x86_64) rid=linux-x64 ;;
  *) echo "unsupported: $(uname -sm)" >&2; exit 1 ;;
esac
out=$here/out/$rid
mkdir -p "$out"

publish() { dotnet publish "$1" -c Release -r "$rid" -o "$out/$2" -nologo -v quiet >/dev/null; }
publish "$repo/src/Rtok.Hook" hook
for p in Client Fat Ef Sqlite; do publish "$here/$p" "$(tr '[:upper:]' '[:lower:]' <<<"$p")"; done

# The store home; bench-linux.sh points it at a volume, not the container overlay.
# The store home. bench-linux.sh puts it on a volume: the container overlay makes fsync slow.
export RTOK_HOME=${T4_HOME:-$out/home}
mkdir -p "$RTOK_HOME" && find "$RTOK_HOME" -mindepth 1 -delete
cp "$here/fixtures/config.toml" "$RTOK_HOME/"
fx=$here/fixtures/pretooluse.json
sock=$RTOK_HOME/run/hook.sock
hook=$out/hook/rtok-hook
client=$out/client/hook-client
fat=$out/fat/rtok-probe

results=$out/results.md
{
  echo "# T4 on $rid ($(date -u +%Y-%m-%d), $(uname -srm), $runs runs, hyperfine -N)"
  echo
  echo "| Probe | p50 ms | p95 ms | mean ms |"
  echo "|---|---|---|---|"
} >"$results"

# bench <label> <command...>: stdin is the PreToolUse fixture.
bench() {
  local label=$1; shift
  local json=$out/bench.json
  hyperfine -N --warmup 20 --runs "$runs" --input "$fx" --export-json "$json" "$(printf '%q ' "$@")" >/dev/null 2>&1
  jq -r --arg l "$label" '.results[0] | (.times | sort) as $t | ($t | length) as $n
    | "| \($l) | \($t[($n - 1) * 0.5 | floor] * 1000 * 100 | round / 100) | \($t[($n - 1) * 0.95 | floor] * 1000 * 100 | round / 100) | \(.mean * 1000 * 100 | round / 100) |"' "$json" >>"$results"
  tail -1 "$results"
}

bench "process floor (true)" "$(command -v true)"
bench "empty rtok-hook" "$hook"
bench "full binary, return at Main" "$fat" --probe-exit-at-main
bench "full rtok hook + config load" "$fat" hook PreToolUse
bench "full rtok hook + config + EF store" "$fat" hook PreToolUse --store
bench "EF Core compiled model: open, insert, query" "$out/ef/ef-probe" "$RTOK_HOME/ef.db"
bench "Microsoft.Data.Sqlite: open, insert, query" "$out/sqlite/sqlite-probe" "$RTOK_HOME/sqlite.db"
bench "fallback a: socket down, no-op + detached spawn" "$client" "$sock" noop "$fat" --probe-exit-at-main
bench "fallback a + request queued for the resident" "$client" "$sock" queue "$fat" --probe-exit-at-main
bench "fallback b: socket down, exec full hook + store" "$client" "$sock" exec "$fat" hook PreToolUse --store

"$fat" serve --socket "$sock" &
pid=$!
until [ -S "$sock" ]; do sleep 0.05; done
bench "rtok-hook + socket round trip (resident no-op)" "$client" "$sock" noop /usr/bin/true
kill "$pid"; wait "$pid" 2>/dev/null || true

"$fat" serve --socket "$sock" --store &
pid=$!
until [ -S "$sock" ]; do sleep 0.05; done
bench "rtok-hook + socket round trip (resident writes calls row)" "$client" "$sock" noop /usr/bin/true
kill "$pid"; wait "$pid" 2>/dev/null || true

# SQLite linked into the binary: StaticLink.e_sqlite3 everywhere, SourceGear's own .a on Linux.
static() {
  local dir=$out/static-$1
  if ! dotnet publish "$here/Sqlite" -c Release -r "$rid" -o "$dir" -p:StaticSqlite="$1" -nologo -v quiet >"$dir.log" 2>&1; then
    echo "| sqlite-probe, StaticSqlite=$1 | publish failed: $(grep -m1 -oE 'error [^[]*' "$dir.log") | | |" >>"$results"
    return
  fi
  rm -f "${dir:?}"/libe_sqlite3.* "${dir:?}"/e_sqlite3.*
  bench "sqlite-probe, StaticSqlite=$1 ($("$dir/sqlite-probe" "$RTOK_HOME/fts-$1.db" fts5), $(wc -c <"$dir/sqlite-probe" | tr -d ' ') bytes, no native lib beside it)" "$dir/sqlite-probe" "$RTOK_HOME/static-$1.db"
}
static true
[ "${rid%%-*}" = linux ] && static v3

# Exec to Main vs spawn to exit: hook-client spawns each binary and the child logs when Main starts.
{
  echo
  echo "| Spawned binary | exec→Main p50 ms | exec→Main p95 ms | spawn→exit p50 ms | spawn→exit p95 ms |"
  echo "|---|---|---|---|---|"
  for b in "$client" "$fat"; do
    read -r m50 m95 t50 t95 < <("$client" --probe-launch "$runs" "$out/t0.log" "$b" $([ "$b" = "$fat" ] && echo --probe-exit-at-main))
    echo "| $(basename "$b") | $m50 | $m95 | $t50 | $t95 |"
  done
} | tee -a "$results"


{
  echo
  echo "| Binary | Bytes |"
  echo "|---|---|"
  for f in "$hook" "$client" "$fat" "$out/ef/ef-probe" "$out/sqlite/sqlite-probe"; do
    echo "| $(basename "$f") | $(wc -c <"$f" | tr -d ' ') |"
  done
  echo
  echo "SQLite native next to the binaries: $(cd "$out/fat" && ls | grep -iE 'e_sqlite3' || echo none)"
} >>"$results"
echo "wrote $results"
