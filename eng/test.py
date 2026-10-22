#!/usr/bin/env python3
"""Runs the JIT tests a change touches, in parallel (plan/T3).

Usage:
  python3 eng/test.py                  # tests affected by changes since origin/main (commits,
                                       # staged, unstaged and untracked files)
  python3 eng/test.py --base main      # diff against another base
  python3 eng/test.py --all            # every JIT test project
  python3 eng/test.py --dry-run        # only print the affected projects
  python3 eng/test.py -- --filter-class '*Walker*'   # extra args go to dotnet test

The affected set comes from eng/affected-tests.sh (the same rules CI uses on pull requests):
a changed file affects its project and every project that references it; a change outside
any project runs everything; docs and the plan run nothing. dotnet test (Microsoft Testing
Platform) then runs the test modules in parallel, and xunit.runner.json parallelizes inside
each module.
"""

from __future__ import annotations

import argparse
import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FILTER = ROOT / "artifacts" / "affected.slnf"


def git(*args: str) -> list[str]:
    out = subprocess.run(
        ["git", *args], cwd=ROOT, check=True, capture_output=True, text=True
    ).stdout
    return [line for line in out.splitlines() if line]


def changed_files(base: str) -> list[str]:
    merge_base = git("merge-base", base, "HEAD")[0]
    files = set(git("diff", "--name-only", merge_base))  # commits + working tree
    files.update(git("ls-files", "--others", "--exclude-standard"))  # untracked
    return sorted(files)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--base", default="origin/main", help="diff base (default origin/main)")
    parser.add_argument("--all", action="store_true", help="run every JIT test project")
    parser.add_argument("-c", "--configuration", default="Debug")
    parser.add_argument("--no-build", action="store_true", help="pass --no-build to dotnet test")
    parser.add_argument("--dry-run", action="store_true", help="only print the affected projects")
    parser.add_argument("rest", nargs=argparse.REMAINDER, help="-- extra dotnet test arguments")
    args = parser.parse_args()

    script = ["bash", str(ROOT / "eng" / "affected-tests.sh"), str(FILTER)]
    if args.all:
        subprocess.run([*script, "--all"], cwd=ROOT, check=True)
    else:
        files = changed_files(args.base)
        subprocess.run(script, cwd=ROOT, check=True, input="\n".join(files) + "\n", text=True)

    if '"projects": [\n    ]' in FILTER.read_text(encoding="utf-8"):
        print("nothing to test")
        return 0
    if args.dry_run:
        return 0

    cmd = ["dotnet", "test", "--solution", str(FILTER), "-c", args.configuration]
    if args.no_build:
        cmd.append("--no-build")
    extra = args.rest[1:] if args.rest[:1] == ["--"] else args.rest
    cmd += extra
    print("+ " + " ".join(cmd), flush=True)
    return subprocess.run(cmd, cwd=ROOT, env=os.environ).returncode


if __name__ == "__main__":
    sys.exit(main())
