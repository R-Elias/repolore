#!/usr/bin/env bash
# Installs one exact .nupkg from a local-only feed into an isolated pinned manifest and runs a short smoke.
# Usage: bash build/smoke-exact-package.sh <path-to-nupkg> <expected-version>
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "smoke-exact-package: usage: <path-to-nupkg> <expected-version>" >&2
  exit 2
fi
if [ ! -f "$1" ]; then
  echo "smoke-exact-package: no such package: $1" >&2
  exit 2
fi

nupkg="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
expected_version="$2"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
mkdir -p "$work/feed" "$work/consumer/.config"
cp "$nupkg" "$work/feed/"

cat > "$work/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="exact-package-only" value="$work/feed" />
  </packageSources>
</configuration>
EOF

cat > "$work/consumer/.config/dotnet-tools.json" <<EOF
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "repolore.cli": { "version": "$expected_version", "commands": [ "repolore" ], "rollForward": false }
  }
}
EOF

export NUGET_PACKAGES="$work/packages"
export NUGET_HTTP_CACHE_PATH="$work/http-cache"
export NUGET_PLUGINS_CACHE_PATH="$work/plugins-cache"
export DOTNET_CLI_HOME="$work/dotnet-home"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

cd "$work/consumer"
dotnet tool restore --configfile "$work/nuget.config"

reported="$(dotnet tool run repolore -- version)"
echo "$reported"
if ! echo "$reported" | grep -qxF "RepoLore $expected_version"; then
  echo "smoke-exact-package: installed tool does not report version $expected_version" >&2
  exit 1
fi

dotnet tool run repolore -- init
dotnet tool run repolore -- health-check
test -f "$work/consumer/_repolore/repolore.json"
echo "smoke-exact-package: passed for $(basename "$nupkg")"
