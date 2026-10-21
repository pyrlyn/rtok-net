#!/usr/bin/env bash
# Scaffold packages/<Name>/ in the shape packages/README.md describes and add it to rtok.slnx.
# Usage: eng/new-package.sh <Pyrlyn.Name|Rtok.Name> "<one-line description>"
set -euo pipefail

name=${1:-}
description=${2:-}
if [[ ! $name =~ ^(Pyrlyn|Rtok)(\.[A-Z][A-Za-z0-9]*)+$ || -z $description ]]; then
  echo "usage: eng/new-package.sh <Pyrlyn.Name|Rtok.Name> \"<one-line description>\"" >&2
  exit 2
fi

root=$(cd "$(dirname "$0")/.." && pwd)
dir=$root/packages/$name
if [[ -e $dir ]]; then
  echo "packages/$name already exists" >&2
  exit 1
fi

mkdir -p "$dir/src" "$dir/tests"
cp "$root/LICENSE" "$dir/LICENSE"

cat >"$dir/src/$name.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <Version>0.1.0</Version>
    <Description>$description</Description>
    <PackageTags></PackageTags>
  </PropertyGroup>
</Project>
EOF

cat >"$dir/tests/$name.Tests.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../src/$name.csproj" />
  </ItemGroup>
</Project>
EOF

cat >"$dir/$name.slnx" <<EOF
<Solution>
  <Project Path="src/$name.csproj" />
  <Project Path="tests/$name.Tests.csproj" />
</Solution>
EOF

cat >"$dir/README.md" <<EOF
# $name

$description

## Install

\`\`\`bash
dotnet add package $name
\`\`\`

## Use

TODO: a short example.
EOF

cat >"$dir/CHANGELOG.md" <<EOF
# Changelog

## 0.1.0

- First release.
EOF

dotnet sln "$root/rtok.slnx" add --solution-folder "packages/$name" \
  "$dir/src/$name.csproj" "$dir/tests/$name.Tests.csproj" >/dev/null
echo "created packages/$name"
