# build

Scripts that run in CI or at build time, outside the shipped code. They may use the network and subprocesses; the shipped CLI may not ([src](../src/src.md)). Anything under `build/` is also excluded from source discovery by default, so it needs no `.repoloreignore` rule.

- `CheckRuntimeBoundary.cs` is the IL check behind the build guard. `Directory.Build.targets` loads it as an inline MSBuild task, so it is compiled by MSBuild, not by the solution.
- `distribution.py` (standard library only) turns one native executable into the archive, the PyPI wheel and the npm packages, and, once all platforms are in, writes the checksum files, the native SBOM, the Homebrew formula and the winget manifests. Text files it writes use LF: a CRLF shebang breaks the npm launcher. The wheel's platform tag is computed from the executable (newest glibc symbol on Linux, minimum OS on macOS), not guessed.
- `npm-publish.sh` publishes the npm tarballs, platform packages first. Tarball paths must start with `./` or `/`: npm reads `release/x.tgz` as a GitHub repository, which is how the 1.1.0 npm job failed. The `assemble` job runs it with `--dry-run` in every publish run, so the real command is exercised before a tag.
- `smoke-exact-package.sh` installs one `.nupkg` from a local-only feed (the package ID comes from the file name) and runs a short journey; `smoke-native.sh` does the same for a native executable.
- `run-without-network.sh` runs a command in a Linux network namespace with loopback only, and refuses to continue if outbound TCP or DNS still works. Linux, as root.
- `clone-reference-repo.sh` clones the scenario reference repository at its pin ([Scenarios](../tests/RepoLore.Cli.Tests/Scenarios/Scenarios.md)).
- `native/` holds the npm launcher (the only code the npm channel adds) and the .NET license text that ships in every archive.

Channels, names and limits: [distribution](../../roadmap/distribution.md).
