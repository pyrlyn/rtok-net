#!/usr/bin/env bash
# Packs one RID's published binaries into its release archive (plan/T3).
# Usage: eng/package-release.sh <rid> <native-rtok> [--allow-missing]
#   <rid>          a RID published by eng/publish.sh into artifacts/dist/<rid>/
#   <native-rtok>  an rtok this host can run; it writes the man pages and completions
#                  (they are the same for every RID)
#   --allow-missing  pack what exists and warn about the rest (CI); a release fails instead
# Writes artifacts/release/rtok-<rid>.tar.gz (.zip for win-*) and its .sha256. The archive
# holds one rtok-<rid>/ directory:
#   rtok, rtok-hook                  the binaries (.exe on Windows)
#   share/man/man1/*.1               rtok man
#   share/completions/*              rtok completions <shell>
#   plugins/ skills/ rules/ modes/   host plugin trees and data files from the repo
#   LICENSE
set -euo pipefail

rid=${1:-}
rtok=${2:-}
allow_missing=false
[[ ${3:-} == --allow-missing ]] && allow_missing=true
if [[ -z $rid || -z $rtok || (-n ${3:-} && ${3:-} != --allow-missing) ]]; then
  echo "usage: eng/package-release.sh <rid> <native-rtok> [--allow-missing]" >&2
  exit 2
fi
root=$(cd "$(dirname "$0")/.." && pwd)
cd "$root"
rtok=$(cd "$(dirname "$rtok")" && pwd)/$(basename "$rtok")

missing() {
  if $allow_missing; then
    echo "warning: $rid archive has no $1" >&2
  else
    echo "error: $rid archive needs $1" >&2
    exit 1
  fi
}

ext=""
[[ $rid == win-* ]] && ext=.exe
name=rtok-$rid
stage=artifacts/release/stage/$name
rm -rf "$stage"
mkdir -p "$stage"

for exe in rtok rtok-hook; do
  [[ -f artifacts/dist/$rid/$exe$ext ]] || {
    echo "error: artifacts/dist/$rid/$exe$ext not found; run eng/publish.sh $rid" >&2
    exit 1
  }
  cp "artifacts/dist/$rid/$exe$ext" "$stage/"
done
cp LICENSE "$stage/"

# Man pages and completions come from the CLI's own command model (T12.11).
commands=$("$rtok" --help 2>&1 || true)
if grep -qE '^ +man( |$)' <<<"$commands"; then
  mkdir -p "$stage/share/man/man1"
  "$rtok" man --dir "$stage/share/man/man1"
  compgen -G "$stage/share/man/man1/*.1" >/dev/null || missing "man pages (rtok man wrote none)"
else
  missing "share/man/man1 (rtok has no man command yet)"
fi
if grep -qE '^ +completions( |$)' <<<"$commands"; then
  mkdir -p "$stage/share/completions"
  for pair in bash:rtok.bash zsh:_rtok fish:rtok.fish powershell:rtok.ps1 clink:rtok.lua; do
    "$rtok" completions "${pair%%:*}" >"$stage/share/completions/${pair#*:}"
  done
else
  missing "share/completions (rtok has no completions command yet)"
fi

for dir in plugins skills rules modes; do
  if [[ -d $dir ]]; then
    cp -R "$dir" "$stage/"
  else
    missing "$dir/"
  fi
done

out=artifacts/release
if [[ $rid == win-* ]]; then
  archive=$name.zip
  rm -f "$out/$archive"
  (cd "$out/stage" && 7z a -tzip -bso0 "../$archive" "$name")
else
  archive=$name.tar.gz
  tar -C "$out/stage" -czf "$out/$archive" "$name"
fi
(
  cd "$out"
  if command -v sha256sum >/dev/null; then
    sha256sum "$archive" >"$archive.sha256"
  else
    shasum -a 256 "$archive" >"$archive.sha256"
  fi
)
echo "packed $out/$archive"
