#!/usr/bin/env bash
# Runs a short smoke on one native repolore executable in a fresh directory: version, init, health-check, a checkpoint,
# a restore and a context read.
# Usage: bash build/smoke-native.sh <executable> <expected-version>
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "smoke-native: usage: <executable> <expected-version>" >&2
  exit 2
fi
if [ ! -f "$1" ]; then
  echo "smoke-native: no such executable: $1" >&2
  exit 2
fi

executable="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
expected_version="$2"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
cd "$work"

reported="$("$executable" version | head -n 1 | tr -d '\r')"
echo "$reported"
if [ "$reported" != "RepoLore $expected_version" ]; then
  echo "smoke-native: the executable does not report version $expected_version" >&2
  exit 1
fi

"$executable" init
"$executable" health-check
printf '\nA smoke edit.\n' >> _repolore/root.md
"$executable" checkpoint
"$executable" restore 1 --path _repolore/root.md
"$executable" history --json | grep -q '"id":3'
"$executable" context --node _repolore/root.md | grep -q '## _repolore/root.md \[durable\]'
echo "smoke-native: passed for $executable"
