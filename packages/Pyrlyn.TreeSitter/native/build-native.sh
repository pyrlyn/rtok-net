#!/usr/bin/env bash
# Build the tree-sitter runtime and the grammars of sources.txt into one native library per
# RID: a static archive for NativeAOT (DirectPInvoke + NativeLibrary) and a shared library
# for the JIT. Prints the output directory on stdout.
#
# Usage: native/build-native.sh [--rid <rid>]
#   --rid   target RID (default: the host's). Another RID needs CC (and AR) set to a cross
#           compiler, e.g. CC="zig cc -target x86_64-linux-gnu" AR="zig ar".
# Env:
#   PYRLYN_TREESITTER_CACHE  cache root (default: $XDG_CACHE_HOME or ~/.cache, /pyrlyn-treesitter)
#   CC, AR, CFLAGS           compiler, archiver, extra flags
#
# Downloads are verified against sources.txt. The output lands in
# <cache>/<key>/<rid>/, where key hashes sources.txt and this script, so a changed pin or
# flag builds afresh and parallel builds of one key cannot see a half-written directory.
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
sources=$here/sources.txt

host_rid() {
  local os arch
  case "$(uname -s)" in
    Darwin) os=osx ;;
    Linux)
      if ldd --version 2>&1 | grep -qi musl; then os=linux-musl; else os=linux; fi
      ;;
    MINGW* | MSYS* | CYGWIN*) os=win ;;
    *) echo "build-native: unknown OS $(uname -s)" >&2; exit 1 ;;
  esac
  case "$(uname -m)" in
    arm64 | aarch64) arch=arm64 ;;
    x86_64 | amd64) arch=x64 ;;
    *) echo "build-native: unknown CPU $(uname -m)" >&2; exit 1 ;;
  esac
  echo "$os-$arch"
}

rid=$(host_rid)
while [[ $# -gt 0 ]]; do
  case $1 in
    --rid) rid=$2; shift 2 ;;
    *) echo "usage: build-native.sh [--rid <rid>]" >&2; exit 2 ;;
  esac
done

sha256() {
  if command -v sha256sum >/dev/null; then sha256sum "$@"; else shasum -a 256 "$@"; fi
}

cache=${PYRLYN_TREESITTER_CACHE:-${XDG_CACHE_HOME:-$HOME/.cache}/pyrlyn-treesitter}
key=$(cat "$sources" "$0" | sha256 | cut -c1-16)
out=$cache/$key/$rid
if [[ -f $out/.done ]]; then
  echo "$out"
  exit 0
fi

case $rid in
  win-*) obj=obj; static=pyrlyn_tree_sitter.lib; shared=pyrlyn_tree_sitter.dll ;;
  osx-*) obj=o; static=libpyrlyn_tree_sitter.a; shared=libpyrlyn_tree_sitter.dylib ;;
  *) obj=o; static=libpyrlyn_tree_sitter.a; shared=libpyrlyn_tree_sitter.so ;;
esac

# Apple clang builds both macOS architectures (-arch below); other targets need a cross compiler.
if [[ $rid != "$(host_rid)" && -z ${CC:-} && ! ($rid == osx-* && $(host_rid) == osx-*) ]]; then
  echo "build-native: $rid is not the host RID; set CC (and AR) to a cross compiler" >&2
  exit 1
fi
case $rid in
  win-*) cc=${CC:-clang}; ar=${AR:-llvm-lib} ;;
  *) cc=${CC:-cc}; ar=${AR:-ar} ;;
esac

# Fetch and verify every crate once per cache root.
mkdir -p "$cache/crates"
work=$(mktemp -d "$cache/build.XXXXXX")
trap 'rm -rf "$work"' EXIT
mkdir -p "$work/src" "$work/obj" "$work/out"
jobs=()
while read -r crate version digest files; do
  [[ -z $crate || $crate == \#* ]] && continue
  tarball=$cache/crates/$crate-$version.crate
  if [[ ! -f $tarball ]] || ! echo "$digest  $tarball" | sha256 -c - >/dev/null 2>&1; then
    curl -sSfL --retry 3 -o "$tarball.part" \
      "https://static.crates.io/crates/$crate/$crate-$version.crate"
    if ! echo "$digest  $tarball.part" | sha256 -c - >/dev/null 2>&1; then
      echo "build-native: $crate $version does not match its sha256 in sources.txt" >&2
      rm -f "$tarball.part"
      exit 1
    fi
    mv "$tarball.part" "$tarball"
  fi
  tar -xzf "$tarball" -C "$work/src"
  for file in $files; do
    jobs+=("$crate-$version/$file")
  done
done <"$sources"

# One object per C file, compiled in parallel. The runtime needs its include paths and
# feature macros; a grammar includes the tree_sitter/ headers beside its parser.c.
flags=(-c -O2 -std=c11 -fvisibility=hidden -w)
# macOS: the RID's architecture, and the oldest macOS .NET 10 runs on, so ld does not warn
# when NativeAOT links the archive.
target=()
case $rid in
  osx-arm64) target=(-arch arm64 -mmacosx-version-min=12.0) ;;
  osx-x64) target=(-arch x86_64 -mmacosx-version-min=12.0) ;;
esac
flags+=("${target[@]}")
case $rid in
  win-*) flags+=(-D_CRT_SECURE_NO_WARNINGS) ;;
  *) flags+=(-fPIC) ;;
esac
compile() {
  local rel=$1 name
  name=$(echo "$rel" | tr '/.' '__')
  # A grammar's shared scanner header (typescript, php common/) finds tree_sitter/ here.
  local extra=(-I"$(dirname "$work/src/$rel")")
  if [[ $rel == tree-sitter-0.*/src/lib.c ]]; then
    local root=$work/src/${rel%%/*}
    extra+=(-I"$root/include" -I"$root/src" -I"$root/src/wasm"
      -D_POSIX_C_SOURCE=200112L -D_DEFAULT_SOURCE -D_DARWIN_C_SOURCE)
  fi
  # shellcheck disable=SC2086
  $cc "${flags[@]}" ${CFLAGS:-} "${extra[@]}" -o "$work/obj/$name.$obj" "$work/src/$rel"
}
export -f compile
export work obj cc CFLAGS
flags_str="${flags[*]}"
export flags_str
cpus=$(getconf _NPROCESSORS_ONLN 2>/dev/null || echo 4)
printf '%s\n' "${jobs[@]}" | xargs -P "$cpus" -I{} bash -c 'flags=($flags_str); compile "$1"' _ {}

objects=("$work"/obj/*."$obj")
case $rid in
  win-*)
    # The DLL exports the C API and the grammar entry points; MSVC needs them listed.
    {
      echo "EXPORTS"
      grep -oE '\bts_[a-z0-9_]+\(' "$work"/src/tree-sitter-0.*/include/tree_sitter/api.h |
        tr -d '(' | sort -u
      grep -ohE 'TS_PUBLIC const TSLanguage \*tree_sitter_[a-z0-9_]+' \
        "$work"/src/*/src/parser.c "$work"/src/*/*/src/parser.c 2>/dev/null |
        sed 's/.*\*//' | sort -u
    } >"$work/exports.def"
    # shellcheck disable=SC2086
    $ar /nologo /out:"$work/out/$static" "${objects[@]}"
    $cc -shared -o "$work/out/$shared" "${objects[@]}" -Wl,/DEF:"$work/exports.def"
    rm -f "$work/out/"*.exp
    ;;
  osx-*)
    $ar rcs "$work/out/$static" "${objects[@]}"
    $cc "${target[@]}" -dynamiclib -install_name "@rpath/$shared" -o "$work/out/$shared" "${objects[@]}"
    ;;
  *)
    $ar rcs "$work/out/$static" "${objects[@]}"
    $cc -shared -o "$work/out/$shared" "${objects[@]}"
    ;;
esac

grep -vE '^\s*(#|$)' "$sources" | awk '{print $1, $2}' >"$work/out/sources.txt"
touch "$work/out/.done"
mkdir -p "$(dirname "$out")"
[[ -d $out && ! -f $out/.done ]] && rm -rf "$out"
if ! mv "$work/out" "$out" 2>/dev/null; then
  # Another build of the same key won the race; its output is identical.
  [[ -f $out/.done ]] || { echo "build-native: cannot write $out" >&2; exit 1; }
fi
echo "$out"
