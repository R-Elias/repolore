#!/usr/bin/env bash
# Publishes the npm tarballs of one version: the platform packages first, then the package users install, which
# depends on them. A version already on the registry is skipped, so a re-run finishes what a failed run started.
# A prerelease goes under the dist-tag "next". With --dry-run, npm checks each tarball without publishing it.
# Usage: bash build/npm-publish.sh <directory> <version> <prerelease: true|false> [--dry-run]
set -euo pipefail

if [ "$#" -lt 3 ] || [ "$#" -gt 4 ] || { [ "$#" -eq 4 ] && [ "$4" != --dry-run ]; }; then
  echo "npm-publish: usage: <directory> <version> <prerelease: true|false> [--dry-run]" >&2
  exit 2
fi
directory=$1 version=$2 dry_run=${4:-}
tag=latest; [ "$3" = true ] && tag=next

publish() {
  # A path must start with ./ or /: npm reads "release/x.tgz" as the GitHub repository release/x.tgz.
  local tarball
  case "$1" in /*) tarball=$1 ;; *) tarball=./$1 ;; esac
  test -f "$tarball" || { echo "npm-publish: no such tarball: $tarball" >&2; exit 1; }
  local name
  name=$(tar -xOzf "$tarball" package/package.json | node -p 'JSON.parse(require("fs").readFileSync(0)).name')
  if [ "$(npm view "$name@$version" version 2> /dev/null)" = "$version" ]; then
    echo "$name@$version is already published"
  else
    npm publish "$tarball" --access public --tag "$tag" $dry_run
  fi
}

shopt -s nullglob
platforms=("$directory"/repolore-org-cli-*-"$version".tgz)
if [ "${#platforms[@]}" -ne 6 ]; then
  echo "npm-publish: expected 6 platform tarballs for $version in $directory, found ${#platforms[@]}" >&2
  exit 1
fi
for tarball in "${platforms[@]}"; do publish "$tarball"; done
publish "$directory/repolore-cli-$version.tgz"
