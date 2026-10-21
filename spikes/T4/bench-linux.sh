#!/usr/bin/env bash
# Run bench.sh in a Linux container (the .NET AOT SDK image), copying the sources in so the
# host's artifacts/ stay untouched. Usage: spikes/T4/bench-linux.sh [runs] [docker args...]
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
repo=$(cd "$here/../.." && pwd)
runs=${1:-200}
shift || true
mkdir -p "$here/out"
docker "$@" run --rm -v "$repo:/src:ro" -v "$here/out:/results" -v rtok-t4-home:/t4home -e T4_HOME=/t4home mcr.microsoft.com/dotnet/sdk:10.0-noble-aot bash -c "
  set -euo pipefail
  apt-get update -qq >/dev/null && apt-get install -y -qq hyperfine jq >/dev/null
  mkdir /work && cd /src
  tar --exclude=./artifacts --exclude=./spikes/T4/out --exclude=.git -cf - . | tar -C /work -xf -
  cd /work && dotnet --version && ./spikes/T4/bench.sh $runs
  rid=\$(ls spikes/T4/out) && mkdir -p /results/\$rid && cp spikes/T4/out/\$rid/results.md /results/\$rid/
"
