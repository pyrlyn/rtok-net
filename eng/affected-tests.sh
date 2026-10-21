#!/usr/bin/env bash
# Writes a solution filter with the JIT test projects a change touches (plan/T3).
# Usage: git diff --name-only <base>...HEAD | eng/affected-tests.sh <out.slnf>
#        eng/affected-tests.sh <out.slnf> --all
# A changed file belongs to the project whose directory holds it; every project that
# references it, directly or not, is affected too. A change outside any project (build
# props, global.json, eng/, .github/, test fixtures) runs everything; docs and the plan
# run nothing. Prints the chosen projects; the filter lists none when nothing is affected.
# Runs on bash 3.2 (macOS), so no associative arrays: sets are newline-separated strings.
set -euo pipefail

out=${1:-}
mode=${2:-}
if [[ -z $out || (-n $mode && $mode != --all) ]]; then
  echo "usage: eng/affected-tests.sh <out.slnf> [--all]  (changed paths on stdin)" >&2
  exit 2
fi
root=$(cd "$(dirname "$0")/.." && pwd)
cd "$root"

# Every project in the solution, as repo-relative paths.
projects=$(grep -oE 'Path="[^"]+\.csproj"' rtok.slnx | sed -E 's/^Path="(.*)"$/\1/')

# Project references, resolved to repo-relative paths: "<referencing> <referenced>" lines.
edges=""
for p in $projects; do
  dir=$(dirname "$p")
  while IFS= read -r ref; do
    [[ -z $ref ]] && continue
    ref=${ref//\\//}
    target=$(cd "$dir/$(dirname "$ref")" && pwd)/$(basename "$ref")
    edges+="$p ${target#"$root"/}"$'\n'
  done < <(grep -oE '<ProjectReference[^>]*Include="[^"]+"' "$p" | sed -E 's/.*Include="([^"]+)"/\1/' || true)
done

has() { [[ -n $1 ]] && grep -qxF -- "$2" <<<"$1"; }

affected=""
full=false
if [[ $mode == --all ]]; then
  full=true
else
  while IFS= read -r file; do
    [[ -z $file ]] && continue
    owner=""
    for p in $projects; do
      dir=$(dirname "$p")/
      if [[ $file == "$dir"* && ${#dir} -gt ${#owner} ]]; then owner=$dir; fi
    done
    if [[ -n $owner ]]; then
      for p in $projects; do
        if [[ $(dirname "$p")/ == "$owner" ]] && ! has "$affected" "$p"; then affected+="$p"$'\n'; fi
      done
    elif [[ $file == *.md || $file == plan/* || $file == docs/* || $file == spikes/* || $file == LICENSE ]]; then
      continue
    else
      echo "full run: $file is outside every project"
      full=true
      break
    fi
  done
fi

if $full; then
  affected=$projects
else
  # Close over reverse references until nothing new is added.
  grew=true
  while $grew; do
    grew=false
    while read -r from to; do
      [[ -z ${from:-} ]] && continue
      if has "$affected" "$to" && ! has "$affected" "$from"; then
        affected+="$from"$'\n'
        grew=true
      fi
    done <<<"$edges"
  done
fi

tests=""
for p in $projects; do
  if [[ $p == *.Tests.csproj ]] && has "$affected" "$p"; then tests+="$p"$'\n'; fi
done

mkdir -p "$(dirname "$out")"
# The filter names the solution relative to itself; the filter must sit inside the repo.
outdir=$(cd "$(dirname "$out")" && pwd)
if [[ $outdir != "$root" && $outdir != "$root"/* ]]; then
  echo "$out must be inside $root" >&2
  exit 2
fi
rel=rtok.slnx
sub=${outdir#"$root"}
while [[ -n $sub ]]; do
  rel=../$rel
  sub=${sub%/*}
done
{
  printf '{\n  "solution": {\n    "path": "%s",\n    "projects": [' "$rel"
  sep=""
  for t in $tests; do
    printf '%s\n      "%s"' "$sep" "$t"
    sep=","
  done
  printf '\n    ]\n  }\n}\n'
} >"$out"

count=$(grep -c . <<<"$tests" || true)
echo "$count test project(s):"
for t in $tests; do echo "  $t"; done
if [[ -n ${GITHUB_OUTPUT:-} ]]; then echo "count=$count" >>"$GITHUB_OUTPUT"; fi
