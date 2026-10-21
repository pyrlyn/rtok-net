#!/usr/bin/env bash
# NativeAOT publish of rtok and rtok-hook for one RID into artifacts/dist/<rid>/ (plan/T3).
# Usage: eng/publish.sh <rid> [--aot-tests]
#   --aot-tests also publishes the AOT test lane (tests/Rtok.AotTests) next to them, so
#   RTOK_SMOKE_BIN can point at the directory.
# NativeAOT does not cross OSes: osx-* publishes on macOS, win-* on Windows, linux-<arch> on
# Linux. linux-musl-* publishes inside the .NET Alpine AOT SDK image (docker) on any Linux
# host of the same arch.
set -euo pipefail

# The SDK image for musl builds; bump the tag and its digest together.
musl_image=mcr.microsoft.com/dotnet/sdk:10.0.402-alpine3.24-aot@sha256:4bafc89718cc5a590940ee8b0848a16bd54f28426bdd155ad54ee0e07843d9d8

rid=${1:-}
extra=${2:-}
if [[ ! $rid =~ ^(osx|linux|linux-musl|win)-(x64|arm64)$ || (-n $extra && $extra != --aot-tests) ]]; then
  echo "usage: eng/publish.sh <rid> [--aot-tests]" >&2
  exit 2
fi
root=$(cd "$(dirname "$0")/.." && pwd)
out=artifacts/dist/$rid
projects=(src/Rtok.Cli/Rtok.Cli.csproj src/Rtok.Hook/Rtok.Hook.csproj)
[[ $extra == --aot-tests ]] && projects+=(tests/Rtok.AotTests/Rtok.AotTests.csproj)
# The version option prints this commit; taken on the host, where git sees the checkout.
revision=${RTOK_SOURCE_REVISION:-$(git -C "$root" rev-parse --short=9 HEAD 2>/dev/null || echo unknown)}

if [[ $rid == linux-musl-* && ! -f /etc/alpine-release ]]; then
  # Re-run this script inside the Alpine image; the NuGet cache is shared with the host.
  nuget=${NUGET_PACKAGES:-$HOME/.nuget/packages}
  mkdir -p "$nuget"
  exec docker run --rm \
    -v "$root:/src" -v "$nuget:/nuget" -w /src \
    -e NUGET_PACKAGES=/nuget -e CI="${CI:-}" -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    -e HOST_UID="$(id -u)" -e HOST_GID="$(id -g)" -e RTOK_SOURCE_REVISION="$revision" \
    "$musl_image" sh -c 'apk add --no-cache -q bash && bash eng/publish.sh "$@"' sh "$@"
fi

cd "$root"
rm -rf "$out"
for project in "${projects[@]}"; do
  dotnet publish "$project" -c Release -r "$rid" -warnaserror -nologo -o "$out" \
    -p:SourceRevisionId="$revision"
done

# Inside the container everything was written as root; hand it back to the host user.
if [[ -n ${HOST_UID:-} ]]; then
  chown -R "$HOST_UID:${HOST_GID:-$HOST_UID}" artifacts "${NUGET_PACKAGES:-/nuget}"
fi

ext=""
[[ $rid == win-* ]] && ext=.exe
for exe in rtok rtok-hook; do
  [[ -f $out/$exe$ext ]] || {
    echo "publish produced no $out/$exe$ext" >&2
    exit 1
  }
done
echo "published $rid to $out"
