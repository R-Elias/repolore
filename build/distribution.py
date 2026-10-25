#!/usr/bin/env python3
"""Packages the native repolore executables for every distribution channel except NuGet.

Standard library only. Each subcommand writes into an output directory; nothing is uploaded from here.

  archive  <rid> <executable> <version> <out>   repolore-<version>-<rid>.zip (Windows) or .tar.gz
  wheel    <rid> <executable> <version> <out>   the PyPI wheel for that platform (no Python launcher)
  npm      <rid> <executable> <version> <out>   the npm platform package directory
  npm-main <version> <out>                      the npm package that users install (repolore-cli)
  release  <version> <dir>                      checksums, SBOM, Homebrew formula and winget manifests for <dir>

The release checksum file, repolore-<version>.sha256, lists exactly the files attached to the GitHub release: the
archives, the NuGet packages and the SBOMs. Wheels and npm tarballs go to their registries and are listed in
repolore-<version>.packages.sha256, which the publish jobs check before uploading.
"""

import base64
import gzip
import hashlib
import io
import json
import os
import re
import struct
import subprocess
import sys
import tarfile
import zipfile

REPOSITORY = "https://github.com/R-Elias/repolore"
WEBSITE = "https://repolore.org"
SUMMARY = "Local, offline operational memory for developers and coding agents, stored as Markdown."
NPM_MAIN = "repolore-cli"
NPM_SCOPE = "@repolore-org"
WINGET_ID = "RepoLore.RepoLore"
HOMEBREW_CLASS = "Repolore"
HOMEBREW_DESC = "Local, offline operational memory for repositories, stored as Markdown"  # under 80 characters
RIDS = ["win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"]
NODE_PLATFORM = {"win": "win32", "linux": "linux", "osx": "darwin"}
# A fixed time for archive entries, so the same executable always gives the same archive bytes.
FIXED_TIME = 315532800  # 1980-01-01, the earliest time a zip entry can hold

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def main(argv):
    if len(argv) < 2 or argv[1] not in COMMANDS:
        sys.exit(__doc__)
    COMMANDS[argv[1]](*argv[2:])


# ---- archives ------------------------------------------------------------------------------------------------------

def archive(rid, executable, version, out):
    check_rid(rid)
    os.makedirs(out, exist_ok=True)
    files = [(executable_name(rid), read(executable), True)] + license_files()
    base = os.path.join(out, f"repolore-{version}-{rid}")
    if rid.startswith("win"):
        path = base + ".zip"
        with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as z:
            for name, data, is_executable in files:
                z.writestr(zip_info(name, is_executable), data)
    else:
        path = base + ".tar.gz"
        raw = io.BytesIO()
        with tarfile.open(fileobj=raw, mode="w", format=tarfile.PAX_FORMAT) as t:
            for name, data, is_executable in files:
                info = tarfile.TarInfo(name)
                info.size, info.mtime, info.mode = len(data), FIXED_TIME, 0o755 if is_executable else 0o644
                t.addfile(info, io.BytesIO(data))
        with open(path, "wb") as f:
            with gzip.GzipFile(fileobj=f, mode="wb", mtime=FIXED_TIME, filename="") as g:
                g.write(raw.getvalue())
    print(path)


def license_files():
    notices = "\n".join([
        "The native RepoLore executables contain the .NET runtime, compiled ahead of time with NativeAOT.",
        "",
        "RepoLore itself is licensed under the Apache License 2.0 (see LICENSE).",
        "The .NET runtime is licensed under the MIT License:",
        "",
        read_text(os.path.join(ROOT, "build", "native", "DOTNET-LICENSE.txt")),
        "",
        "The .NET runtime's own third-party notices follow, as shipped with the compiler package used for this build.",
        "",
        read_text(runtime_notices()),
    ])
    return [("LICENSE", read_text(os.path.join(ROOT, "LICENSE")).encode("utf-8"), False),
            ("THIRD-PARTY-NOTICES.txt", notices.encode("utf-8"), False)]


def runtime_notices():
    packages = subprocess.run(["dotnet", "nuget", "locals", "global-packages", "--list"],
                              capture_output=True, text=True, check=True).stdout
    root = packages.split(":", 1)[1].strip()
    version = compiler_version()
    path = os.path.join(root, "microsoft.dotnet.ilcompiler", version, "THIRD-PARTY-NOTICES.TXT")
    if not os.path.isfile(path):
        sys.exit(f"distribution: {path} is missing; run the native restore first")
    return path


def compiler_version():
    lock = json.loads(read_text(os.path.join(ROOT, "src", "RepoLore.Cli", "packages.native.lock.json")))
    return lock["dependencies"]["net10.0"]["Microsoft.DotNet.ILCompiler"]["resolved"]


# ---- PyPI ----------------------------------------------------------------------------------------------------------

def wheel(rid, executable, version, out):
    check_rid(rid)
    os.makedirs(out, exist_ok=True)
    data = read(executable)
    tag = "py3-none-" + wheel_platform(rid, data)
    pep440 = python_version(version)
    name = f"repolore-{pep440}"
    info = f"{name}.dist-info"
    metadata = "\n".join([
        "Metadata-Version: 2.4",
        "Name: repolore",
        f"Version: {pep440}",
        f"Summary: {SUMMARY}",
        "License-Expression: Apache-2.0",
        "License-File: LICENSE",
        "License-File: THIRD-PARTY-NOTICES.txt",
        f"Project-URL: Homepage, {WEBSITE}",
        f"Project-URL: Source, {REPOSITORY}",
        f"Project-URL: Documentation, {REPOSITORY}/blob/main/docs/user-guide.md",
        "Description-Content-Type: text/markdown",
        "",
        package_readme("pip install repolore"),
    ])
    entries = [(f"{name}.data/scripts/{executable_name(rid)}", data, True)]
    entries += [(f"{info}/licenses/{n}", d, False) for n, d, _ in license_files()]
    entries += [(f"{info}/METADATA", metadata.encode("utf-8"), False),
                (f"{info}/WHEEL", f"Wheel-Version: 1.0\nGenerator: repolore-distribution\nRoot-Is-Purelib: false\nTag: {tag}\n".encode(), False)]
    record = "".join(f"{n},sha256={urlsafe_hash(d)},{len(d)}\n" for n, d, _ in entries) + f"{info}/RECORD,,\n"
    entries.append((f"{info}/RECORD", record.encode(), False))
    path = os.path.join(out, f"{name}-{tag}.whl")
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as z:
        for entry_name, entry_data, is_executable in entries:
            z.writestr(zip_info(entry_name, is_executable), entry_data)
    print(path)


def wheel_platform(rid, data):
    if rid == "win-x64":
        return "win_amd64"
    if rid == "win-arm64":
        return "win_arm64"
    if rid.startswith("linux"):
        # The executable links glibc dynamically; the wheel claims the newest glibc symbol version it needs.
        versions = {tuple(map(int, v)) for v in re.findall(rb"GLIBC_(\d+)\.(\d+)", data)}
        if not versions:
            sys.exit("distribution: no glibc symbol versions found in the Linux executable")
        major, minor = max(versions)
        return f"manylinux_{major}_{minor}_" + ("x86_64" if rid == "linux-x64" else "aarch64")
    major, minor = macos_minimum(data)
    return f"macosx_{major}_{0 if major >= 11 else minor}_" + ("x86_64" if rid == "osx-x64" else "arm64")


def macos_minimum(data):
    magic, _, _, _, ncmds, _, _ = struct.unpack_from("<IiiIIII", data, 0)
    if magic != 0xFEEDFACF:
        sys.exit("distribution: the macOS executable is not a 64-bit Mach-O file")
    offset = 32
    for _ in range(ncmds):
        cmd, size = struct.unpack_from("<II", data, offset)
        if cmd == 0x32:  # LC_BUILD_VERSION: platform, minos
            minos = struct.unpack_from("<I", data, offset + 12)[0]
            return minos >> 16, (minos >> 8) & 0xFF
        if cmd == 0x24:  # LC_VERSION_MIN_MACOSX
            minos = struct.unpack_from("<I", data, offset + 8)[0]
            return minos >> 16, (minos >> 8) & 0xFF
        offset += size
    sys.exit("distribution: the macOS executable states no minimum OS version")


def python_version(version):
    match = re.fullmatch(r"(\d+\.\d+\.\d+)(?:-(rc|alpha|beta)\.(\d+))?", version)
    if not match:
        sys.exit(f"distribution: version {version} has no PEP 440 form")
    core, label, number = match.groups()
    return core if label is None else core + {"rc": "rc", "alpha": "a", "beta": "b"}[label] + number


# ---- npm -----------------------------------------------------------------------------------------------------------

def npm(rid, executable, version, out):
    check_rid(rid)
    os_name, arch = rid.split("-")
    directory = os.path.join(out, npm_platform_name(rid).split("/")[1])
    os.makedirs(os.path.join(directory, "bin"), exist_ok=True)
    target = os.path.join(directory, "bin", executable_name(rid))
    write(target, read(executable))
    os.chmod(target, 0o755)
    for name, data, _ in license_files():
        write(os.path.join(directory, name), data)
    manifest = {
        "name": npm_platform_name(rid),
        "version": version,
        "description": f"The repolore executable for {NODE_PLATFORM[os_name]}-{arch}. Install {NPM_MAIN} instead.",
        "license": "Apache-2.0",
        "homepage": WEBSITE,
        "repository": {"type": "git", "url": f"git+{REPOSITORY}.git"},
        "os": [NODE_PLATFORM[os_name]],
        "cpu": [arch],
        "files": ["bin/", "LICENSE", "THIRD-PARTY-NOTICES.txt"],
        "preferUnplugged": True,
    }
    write(os.path.join(directory, "package.json"), (json.dumps(manifest, indent=2) + "\n").encode())
    print(directory)


def npm_main(version, out):
    directory = os.path.join(out, NPM_MAIN)
    os.makedirs(os.path.join(directory, "bin"), exist_ok=True)
    # Text files are written with LF whatever the checkout used: a CRLF shebang line would break the launcher.
    write(os.path.join(directory, "bin", "repolore.js"), read_text(os.path.join(ROOT, "build", "native", "npm-launcher.js")).encode("utf-8"))
    write(os.path.join(directory, "LICENSE"), read_text(os.path.join(ROOT, "LICENSE")).encode("utf-8"))
    write(os.path.join(directory, "README.md"), package_readme(f"npm install -g {NPM_MAIN}").encode())
    manifest = {
        "name": NPM_MAIN,
        "version": version,
        "description": SUMMARY,
        "license": "Apache-2.0",
        "homepage": WEBSITE,
        "repository": {"type": "git", "url": f"git+{REPOSITORY}.git"},
        "keywords": ["repolore", "knowledge", "markdown", "cli", "agents"],
        "bin": {"repolore": "bin/repolore.js"},
        "files": ["bin/", "LICENSE", "README.md"],
        "engines": {"node": ">=18"},
        "optionalDependencies": {npm_platform_name(rid): version for rid in RIDS},
    }
    write(os.path.join(directory, "package.json"), (json.dumps(manifest, indent=2) + "\n").encode())
    print(directory)


def npm_platform_name(rid):
    os_name, arch = rid.split("-")
    return f"{NPM_SCOPE}/cli-{NODE_PLATFORM[os_name]}-{arch}"


# ---- release files -------------------------------------------------------------------------------------------------

def release(version, directory):
    names = sorted(n for n in os.listdir(directory)
                   if os.path.isfile(os.path.join(directory, n)) and not n.endswith(".sha256") and not n.endswith(".native.cdx.json"))
    sums = {n: sha256(os.path.join(directory, n)) for n in names}
    archives = {rid: f"repolore-{version}-{rid}." + ("zip" if rid.startswith("win") else "tar.gz") for rid in RIDS}
    missing = [n for n in archives.values() if n not in sums]
    if missing:
        sys.exit(f"distribution: missing archives: {', '.join(missing)}")

    sbom = native_sbom(version, {rid: sums[name] for rid, name in archives.items()})
    sbom_name = f"repolore-{version}.native.cdx.json"
    write(os.path.join(directory, sbom_name), (json.dumps(sbom, indent=2) + "\n").encode())
    sums[sbom_name] = sha256(os.path.join(directory, sbom_name))

    is_package = lambda n: n.endswith(".whl") or n.endswith(".tgz")
    checksum_name = f"repolore-{version}.sha256"
    write(os.path.join(directory, checksum_name), "".join(f"{sums[n]}  {n}\n" for n in sorted(sums) if not is_package(n)).encode())
    write(os.path.join(directory, f"repolore-{version}.packages.sha256"),
          "".join(f"{sums[n]}  {n}\n" for n in sorted(sums) if is_package(n)).encode())

    channels = os.path.join(directory, "channels")
    url = lambda rid: f"{REPOSITORY}/releases/download/v{version}/{archives[rid]}"
    write(os.path.join(channels, "homebrew", "repolore.rb"), homebrew_formula(version, url, {r: sums[archives[r]] for r in RIDS}).encode())
    winget_dir = os.path.join(channels, "winget", WINGET_ID.split(".")[0][0].lower(), *WINGET_ID.split("."), version)
    for name, text in winget_manifests(version, url, {r: sums[archives[r]] for r in RIDS}).items():
        write(os.path.join(winget_dir, name), text.encode())
    print(os.path.join(directory, checksum_name))


def native_sbom(version, archive_hashes):
    compiler = compiler_version()
    component = lambda name, ver, license_id, purl: {
        "type": "library", "name": name, "version": ver, "purl": purl,
        "licenses": [{"license": {"id": license_id}}]}
    return {
        "bomFormat": "CycloneDX",
        "specVersion": "1.6",
        "version": 1,
        "metadata": {
            "component": {
                "type": "application", "name": "repolore", "version": version,
                "licenses": [{"license": {"id": "Apache-2.0"}}],
                "externalReferences": [{"type": "vcs", "url": REPOSITORY}],
                "properties": [{"name": f"archive:{rid}", "value": f"sha256:{h}"} for rid, h in sorted(archive_hashes.items())],
            }
        },
        "components": [
            component("RepoLore.Core", version, "Apache-2.0", f"pkg:nuget/RepoLore.Core@{version}"),
            component("RepoLore.Infrastructure", version, "Apache-2.0", f"pkg:nuget/RepoLore.Infrastructure@{version}"),
            component("Microsoft.DotNet.ILCompiler", compiler, "MIT", f"pkg:nuget/Microsoft.DotNet.ILCompiler@{compiler}"),
        ] + [component(f"runtime.{rid}.Microsoft.DotNet.ILCompiler", compiler, "MIT",
                       f"pkg:nuget/runtime.{rid}.Microsoft.DotNet.ILCompiler@{compiler}") for rid in RIDS],
    }


def homebrew_formula(version, url, sums):
    def block(rid):
        return f'      url "{url(rid)}"\n      sha256 "{sums[rid]}"'
    return f'''class {HOMEBREW_CLASS} < Formula
  desc "{HOMEBREW_DESC}"
  homepage "{WEBSITE}"
  version "{version}"
  license "Apache-2.0"

  on_macos do
    on_arm do
{block("osx-arm64")}
    end
    on_intel do
{block("osx-x64")}
    end
  end

  on_linux do
    on_arm do
{block("linux-arm64")}
    end
    on_intel do
{block("linux-x64")}
    end
  end

  def install
    bin.install "repolore"
  end

  test do
    assert_match "RepoLore #{{version}}", shell_output("#{{bin}}/repolore version")
  end
end
'''


def winget_manifests(version, url, sums):
    common = f"PackageIdentifier: {WINGET_ID}\nPackageVersion: {version}\n"
    tail = "ManifestVersion: 1.6.0\n"
    installers = "".join(
        f"- Architecture: {arch}\n  InstallerUrl: {url(rid)}\n  InstallerSha256: {sums[rid].upper()}\n"
        for arch, rid in (("x64", "win-x64"), ("arm64", "win-arm64")))
    return {
        f"{WINGET_ID}.yaml": common + "DefaultLocale: en-US\nManifestType: version\n" + tail,
        f"{WINGET_ID}.installer.yaml": common
        + "InstallerType: zip\nNestedInstallerType: portable\nNestedInstallerFiles:\n"
        + "- RelativeFilePath: repolore.exe\n  PortableCommandAlias: repolore\n"
        + "Commands:\n- repolore\nInstallers:\n" + installers + "ManifestType: installer\n" + tail,
        f"{WINGET_ID}.locale.en-US.yaml": common
        + "PackageLocale: en-US\nPublisher: Elias Aliche\n"
        + f"PublisherUrl: {WEBSITE}\nPackageName: RepoLore\nPackageUrl: {WEBSITE}\nLicense: Apache-2.0\n"
        + f"LicenseUrl: {REPOSITORY}/blob/main/LICENSE\nShortDescription: {SUMMARY}\n"
        + "Tags:\n- knowledge\n- markdown\n- cli\n- agents\n"
        + f"ReleaseNotesUrl: {REPOSITORY}/releases/tag/v{version}\nManifestType: defaultLocale\n" + tail,
    }


# ---- helpers -------------------------------------------------------------------------------------------------------

def package_readme(install):
    return f"""# RepoLore

{SUMMARY} RepoLore keeps what a maintainer needs to know before changing an area of a repository as plain Markdown
under `_repolore/`, and the `repolore` command reads it, checks it and protects it with local checkpoints. It makes no
network calls and sends no telemetry.

```text
{install}
repolore init
```

This package installs the native `repolore` executable; no .NET installation is needed. See the
[user guide]({REPOSITORY}/blob/main/docs/user-guide.md) and {WEBSITE}.
"""


def check_rid(rid):
    if rid not in RIDS:
        sys.exit(f"distribution: unknown platform {rid}; expected one of {', '.join(RIDS)}")


def executable_name(rid):
    return "repolore.exe" if rid.startswith("win") else "repolore"


def zip_info(name, is_executable):
    info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
    info.compress_type = zipfile.ZIP_DEFLATED
    info.external_attr = ((0o100755 if is_executable else 0o100644) & 0xFFFF) << 16
    return info


def urlsafe_hash(data):
    return base64.urlsafe_b64encode(hashlib.sha256(data).digest()).rstrip(b"=").decode()


def sha256(path):
    return hashlib.sha256(read(path)).hexdigest()


def read(path):
    with open(path, "rb") as f:
        return f.read()


def read_text(path):
    return read(path).decode("utf-8").replace("\r\n", "\n")


def write(path, data):
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    with open(path, "wb") as f:
        f.write(data)


COMMANDS = {"archive": archive, "wheel": wheel, "npm": npm, "npm-main": npm_main, "release": release}

if __name__ == "__main__":
    main(sys.argv)
