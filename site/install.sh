#!/bin/sh
# Installs the native repolore executable on Linux or macOS from the GitHub release, after checking its SHA-256
# against the release's checksum file.
#   curl -fsSL https://repolore.org/install.sh | sh
# Environment: REPOLORE_VERSION (default: the version this script was published with),
#              REPOLORE_INSTALL_DIR (default: $HOME/.local/bin),
#              REPOLORE_DOWNLOAD_BASE (default: the GitHub release; for example an internal mirror of its files).
set -eu

version="${REPOLORE_VERSION:-__VERSION__}"
install_dir="${REPOLORE_INSTALL_DIR:-$HOME/.local/bin}"
base="${REPOLORE_DOWNLOAD_BASE:-https://github.com/R-Elias/repolore/releases/download/v$version}"

fail() { echo "repolore install: $*" >&2; exit 1; }

case "$(uname -s)" in
  Linux) os=linux ;;
  Darwin) os=osx ;;
  *) fail "unsupported system $(uname -s); on Windows use install.ps1" ;;
esac
case "$(uname -m)" in
  x86_64 | amd64) arch=x64 ;;
  arm64 | aarch64) arch=arm64 ;;
  *) fail "unsupported processor $(uname -m)" ;;
esac
if [ "$os" = linux ] && ! ldd --version 2>&1 | grep -qi 'glibc\|gnu libc'; then
  fail "the Linux executable needs glibc; on musl-based systems such as Alpine, install with 'dotnet tool' instead"
fi

archive="repolore-$version-$os-$arch.tar.gz"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT INT TERM

download() {
  if command -v curl >/dev/null 2>&1; then curl -fsSL "$1" -o "$2"
  elif command -v wget >/dev/null 2>&1; then wget -q "$1" -O "$2"
  else fail "neither curl nor wget is available"
  fi
}
download "$base/$archive" "$work/$archive" || fail "could not download $base/$archive"
download "$base/repolore-$version.sha256" "$work/sums" || fail "could not download the checksum file"

expected="$(grep "  $archive\$" "$work/sums" | cut -d ' ' -f 1)"
[ -n "$expected" ] || fail "the checksum file does not list $archive"
if command -v sha256sum >/dev/null 2>&1; then actual="$(sha256sum "$work/$archive" | cut -d ' ' -f 1)"
else actual="$(shasum -a 256 "$work/$archive" | cut -d ' ' -f 1)"
fi
[ "$actual" = "$expected" ] || fail "checksum mismatch for $archive: expected $expected, got $actual"

tar -xzf "$work/$archive" -C "$work"
mkdir -p "$install_dir"
mv "$work/repolore" "$install_dir/repolore"
chmod 755 "$install_dir/repolore"
echo "repolore $version installed to $install_dir/repolore"
case ":$PATH:" in
  *":$install_dir:"*) "$install_dir/repolore" version ;;
  *) echo "Add $install_dir to your PATH to run 'repolore'." ;;
esac
