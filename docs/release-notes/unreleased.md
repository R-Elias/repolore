# Unreleased (next version)

Draft notes for changes after `1.0.0`. Before tagging the next version, move the entries below into `docs/release-notes/<version>.md` (the release workflow refuses a version without its own notes file) and reset this list.

- **New channels:** the same tool now also ships as a native executable that needs no .NET, for Windows, Linux (glibc) and macOS on x64 and arm64: archives on the GitHub release, `repolore` on PyPI, `repolore-cli` on npm, `repolore-org/tap/repolore` on Homebrew, `RepoLore.RepoLore` on winget, and install scripts on repolore.org. Each executable passes the same smoke and scenario suite as the NuGet tool on its own operating system.
- **Release files:** the checksum file is now `repolore-<version>.sha256` and covers every file attached to the release. A second SBOM, `repolore-<version>.native.cdx.json`, lists the .NET runtime components inside the executables, and every archive carries the runtime's license and notices.
- **Publishing:** the publish workflow runs by hand with one checkbox per channel; pushing a tag no longer publishes. The same build can also be published to NuGet under the ID `RepoLore`.
