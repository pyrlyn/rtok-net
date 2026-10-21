#!/usr/bin/env bash
# Checks the Claude and Codex marketplace catalogs (plan/T3). They live in this repo, so a
# merge to main publishes them; the hosts read them from GitHub.
# Usage: eng/check-marketplace.sh [--strict]
#   Each catalog must be valid JSON with one `rtok` plugin whose source is ./plugins/<host>.
#   --strict also requires that directory to exist (a release; T36 carries the trees over).
set -euo pipefail

strict=false
[[ ${1:-} == --strict ]] && strict=true
if [[ -n ${1:-} && ${1:-} != --strict ]]; then
  echo "usage: eng/check-marketplace.sh [--strict]" >&2
  exit 2
fi
cd "$(dirname "$0")/.."

failed=false
check() {
  local host=$1 file=$2 query=$3 source
  if ! source=$(jq -er "$query" "$file" 2>/dev/null); then
    echo "$file: no rtok plugin with a source" >&2
    failed=true
  elif [[ $source != "./plugins/$host" ]]; then
    echo "$file: rtok source is $source, expected ./plugins/$host" >&2
    failed=true
  elif $strict && [[ ! -d plugins/$host ]]; then
    echo "$file: plugins/$host does not exist" >&2
    failed=true
  else
    echo "$host: ok ($source)"
  fi
}

check claude .claude-plugin/marketplace.json '.plugins[] | select(.name == "rtok") | .source'
check codex .agents/plugins/marketplace.json \
  '.plugins[] | select(.name == "rtok") | .source | select(.source == "local") | .path'

if $failed; then exit 1; fi
