#!/usr/bin/env bash
# Fuzzes one rtok-fuzz target under libFuzzer (plan/T53, tests/README.md).
# Usage: eng/fuzz.sh <target> [libFuzzer flags...]   e.g. eng/fuzz.sh jsonc-edit -max_total_time=60
# Publishes tests/Rtok.Fuzz, instruments rtok's and Tomlyn's assemblies with sharpfuzz, builds
# libfuzzer-dotnet from its pinned source with a clang that ships libFuzzer (CLANG=..., e.g.
# Homebrew llvm on macOS; Apple clang has none) and runs it. New inputs land in
# artifacts/fuzz/<target>/corpus, crashes in artifacts/fuzz/<target>/crash-*.
set -euo pipefail

# libfuzzer-dotnet v2025.05.02.0904; bump the commit to update.
libfuzzer_commit=bd39d4e88d715ab460a929943645be2a186cde52

target=${1:-}
if [[ -z $target ]]; then
  echo "usage: eng/fuzz.sh <target> [libFuzzer flags...]; targets: dotnet run --project tests/Rtok.Fuzz -- --list" >&2
  exit 2
fi
shift
root=$(cd "$(dirname "$0")/.." && pwd)
work=$root/artifacts/fuzz
out=$work/$target
seeds=$root/tests/Rtok.Fuzz/corpus/$target
if [[ ! -d $seeds ]]; then
  echo "no corpus for $target at $seeds" >&2
  exit 2
fi

harness=$work/libfuzzer-dotnet-$libfuzzer_commit
if [[ ! -x $harness ]]; then
  mkdir -p "$work"
  curl -fsSL "https://raw.githubusercontent.com/Metalnem/libfuzzer-dotnet/$libfuzzer_commit/libfuzzer-dotnet.cc" \
    -o "$work/libfuzzer-dotnet.cc"
  "${CLANG:-clang}" -fsanitize=fuzzer "$work/libfuzzer-dotnet.cc" -o "$harness"
fi

rm -rf "${out:?}/bin"
dotnet publish "$root/tests/Rtok.Fuzz" -c Release -o "$out/bin" --nologo -v quiet
dotnet tool restore --tool-manifest "$root/.config/dotnet-tools.json" > /dev/null
for dll in "$out"/bin/Pyrlyn.*.dll "$out"/bin/Rtok.*.dll "$out/bin/Tomlyn.dll"; do
  (cd "$root" && dotnet sharpfuzz "$dll")
done

mkdir -p "$out/corpus"
cd "$out"
exec "$harness" --target_path="$out/bin/rtok-fuzz" --target_arg="$target" "$@" "$out/corpus" "$seeds"
