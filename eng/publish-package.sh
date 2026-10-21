#!/usr/bin/env bash
# Test, pack and push one package for a <Name>/v<version> tag (packages/README.md).
# Usage: eng/publish-package.sh <Name>/v<version> [--dry-run]
#   --dry-run pushes to a local folder feed instead of NuGet.
#   Otherwise NUGET_API_KEY is required; NUGET_SOURCE defaults to nuget.org.
set -euo pipefail

tag=${1:-}
dry_run=false
[[ ${2:-} == --dry-run ]] && dry_run=true
if [[ ! $tag =~ ^((Pyrlyn|Rtok)(\.[A-Z][A-Za-z0-9]*)+)/v([0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?)$ ]]; then
  echo "usage: eng/publish-package.sh <Name>/v<version> [--dry-run]" >&2
  exit 2
fi
name=${BASH_REMATCH[1]}
version=${BASH_REMATCH[4]}

root=$(cd "$(dirname "$0")/.." && pwd)
dir=$root/packages/$name
project=$dir/src/$name.csproj
fail() {
  echo "$name: $*" >&2
  exit 1
}

[[ -f $project ]] || fail "packages/$name/src/$name.csproj not found"
declared=$(dotnet msbuild "$project" -getProperty:Version -p:Configuration=Release)
[[ $declared == "$version" ]] || fail "tag says $version, the project says $declared"
grep -qE "^## \[?$version\]?( |$)" "$dir/CHANGELOG.md" || fail "CHANGELOG.md has no '## $version' entry"
if grep -q TODO "$dir/README.md"; then fail "README.md still has a TODO"; fi

out=$root/artifacts/package/$name/$version
rm -rf "$out"
dotnet test --solution "$dir/$name.slnx" -c Release
dotnet pack "$project" -c Release -warnaserror -o "$out"

nupkg=$out/$name.$version.nupkg
[[ -f $nupkg && -f $out/$name.$version.snupkg ]] || fail "pack did not produce $name.$version.nupkg and .snupkg"

if $dry_run; then
  feed=$(mktemp -d)
  trap 'rm -rf "$feed"' EXIT
  dotnet nuget push "$nupkg" --source "$feed"
  [[ -f $feed/$name.$version.nupkg ]] || fail "dry-run push did not land in the local feed"
  echo "$name $version: dry run ok"
else
  [[ -n ${NUGET_API_KEY:-} ]] || fail "NUGET_API_KEY is not set"
  # The .snupkg next to the .nupkg is pushed with it.
  dotnet nuget push "$nupkg" --api-key "$NUGET_API_KEY" \
    --source "${NUGET_SOURCE:-https://api.nuget.org/v3/index.json}" --skip-duplicate
fi
