#!/usr/bin/env bash
# Runs a command inside a Linux network namespace that has only loopback.
# Usage: sudo env "PATH=$PATH" bash build/run-without-network.sh <command> [arguments...]
set -euo pipefail

if [ "$(uname -s)" != "Linux" ]; then
  echo "run-without-network: Linux only" >&2
  exit 2
fi
if [ "$#" -eq 0 ]; then
  echo "run-without-network: no command given" >&2
  exit 2
fi
if [ "$(id -u)" -ne 0 ]; then
  echo "run-without-network: run as root (sudo)" >&2
  exit 2
fi

if [ -z "${REPOLORE_NETWORK_BLOCKED:-}" ]; then
  exec env REPOLORE_NETWORK_BLOCKED=1 unshare --net -- bash "$0" "$@"
fi

ip link set lo up

if (exec 3<>/dev/tcp/1.1.1.1/443) 2>/dev/null; then
  echo "run-without-network: outbound TCP still works; refusing to continue" >&2
  exit 1
fi
if getent hosts api.nuget.org >/dev/null 2>&1; then
  echo "run-without-network: DNS still resolves; refusing to continue" >&2
  exit 1
fi

exec "$@"
