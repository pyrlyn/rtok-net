#!/usr/bin/env bash
# Smoke-tests one RID's rtok and rtok-hook on this runner (plan/T3): `rtok --version` prints
# the version, and rtok-hook answers every hook fixture with exit 0 and `{}`.
# Usage: eng/smoke.sh <rid> <dir>
#   <dir> holds the binaries (artifacts/dist/<rid>, or an unpacked release archive).
# osx-x64 runs under Rosetta on an arm64 Mac; linux-musl-* runs in an Alpine container.
set -euo pipefail

# Plain Alpine for running musl binaries; bump the tag and its digest together.
alpine_image=alpine:3.22@sha256:822614ea121e1d40a33655081af10ef85f0aa2ab9e61f649ceaf0c651822433c

rid=${1:-}
dir=${2:-}
if [[ -z $rid || -z $dir || ! -d $dir ]]; then
  echo "usage: eng/smoke.sh <rid> <dir>" >&2
  exit 2
fi
root=$(cd "$(dirname "$0")/.." && pwd)
dir=$(cd "$dir" && pwd)

if [[ $rid == linux-musl-* && ! -f /etc/alpine-release ]]; then
  exec docker run --rm -v "$root:/src:ro" -v "$dir:/bin-under-test:ro" -w /src "$alpine_image" \
    sh -c 'apk add --no-cache -q bash && bash eng/smoke.sh "$1" /bin-under-test' sh "$rid"
fi

run=()
[[ $rid == osx-x64 && $(uname -m) == arm64 ]] && run=(arch -x86_64)
ext=""
[[ $rid == win-* ]] && ext=.exe

version=$(${run[@]+"${run[@]}"} "$dir/rtok$ext" --version)
if [[ ! $version =~ ^rtok\ [0-9]+\.[0-9]+\.[0-9]+.*\ \([0-9a-f]{9}\)$ ]]; then
  echo "$rid: rtok --version printed '$version'" >&2
  exit 1
fi
echo "$rid: $version"

for fixture in "$root"/tests/fixtures/hooks/*.json; do
  reply=$(${run[@]+"${run[@]}"} "$dir/rtok-hook$ext" <"$fixture")
  reply=${reply%$'\r'}
  if [[ $reply != "{}" ]]; then
    echo "$rid: rtok-hook answered $(basename "$fixture") with '$reply'" >&2
    exit 1
  fi
done
echo "$rid: rtok-hook ok on $(find "$root/tests/fixtures/hooks" -name '*.json' | wc -l | tr -d ' ') fixtures"
