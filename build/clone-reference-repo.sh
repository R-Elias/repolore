#!/usr/bin/env bash
# Clones the scenario reference repository named by the pin file and checks that its tag still points at the pinned
# commit. The scenario tests then use only this local clone.
# Usage: bash build/clone-reference-repo.sh <destination>
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "clone-reference-repo: usage: <destination>" >&2
  exit 2
fi

pin="$(dirname "$0")/../tests/RepoLore.Cli.Tests/Scenarios/reference-repo.version"
repository=$(awk '$1 == "repository" { print $2 }' "$pin")
tag=$(awk '$1 == "tag" { print $2 }' "$pin")
commit=$(awk '$1 == "commit" { print $2 }' "$pin")

git -c core.longpaths=true -c core.autocrlf=false clone --quiet --no-checkout "$repository" "$1"
actual=$(git -C "$1" rev-parse --verify --quiet "$tag^{commit}" || true)
if [ "$actual" != "$commit" ]; then
  echo "clone-reference-repo: tag $tag resolves to '${actual:-nothing}', not the pinned $commit; push the stage, or fix the pin" >&2
  exit 1
fi
