#!/usr/bin/env bash
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
[ -z "$(git status --porcelain)" ] || { echo "ABORT: working tree not clean"; git status --short; exit 1; }
P="$(mktemp)"; trap 'rm -f "$P"' EXIT
cat > "$P"
[ -s "$P" ] || { echo "ABORT: empty patch"; exit 1; }
git apply --check --recount --whitespace=nowarn "$P"
git apply         --recount --whitespace=nowarn "$P"
git diff --stat
