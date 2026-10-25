# Distribution beyond NuGet

Status: **set up on branch `feature/distribution` (2026-10-25); no channel other than NuGet has been published yet.** The first release using it is the one after 1.0.0.

## Decision (maintainer, 2026-10-25)

Not everyone has or wants .NET, so the same tool also ships as a native executable, wrapped for the package managers that companies and developers already use. This replaced the v1 boundary "one NuGet package, no additional package ecosystems". What stays: one CLI, one code base, one tagged commit and one publish workflow for every channel; no separate Core package, plugins or hosted service.

## Channels and names

| Channel | Name | Notes |
|---|---|---|
| NuGet | `RepoLore.Cli`; optionally `RepoLore` | The same build packed twice. Which ID is the main one is still open; NuGet's deprecation with an alternate package can steer users. Request the `RepoLore` ID prefix reservation with NuGet.org |
| GitHub release | `repolore-<version>-<rid>.zip` / `.tar.gz` | `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`; each archive holds the executable, `LICENSE` and `THIRD-PARTY-NOTICES.txt` |
| PyPI | `repolore` | One wheel per platform, the executable in the wheel's scripts folder: no Python launcher, no Python at run time |
| npm | `repolore-cli`, platform packages `@repolore-org/cli-<platform>-<arch>` | `repolore` on npm is an unrelated, similar project (created 2026-07-18) that also installs a `repolore` command. The launcher starts the executable as a child process: the npm channel's addition, not RepoLore's |
| Homebrew | formula `repolore` in the tap `repolore-org/tap` | `brew install repolore-org/tap/repolore`, or `brew install repolore` once the tap is added. A bare `brew install repolore` without the tap needs Homebrew's main catalogue, which requires a well-known project |
| winget | moniker `repolore` (identifier `RepoLore.RepoLore`) | winget identifiers need a `Publisher.Package` form, so `winget install repolore` works through the manifest's moniker. A zip with a nested portable executable; each version is a pull request to `microsoft/winget-pkgs`, reviewed by Microsoft |
| Website | repolore.org | `site/`: a download page plus `install.sh` and `install.ps1`, which check the archive against the release checksums. Deployed to GitHub Pages for now; the maintainer plans Cloudflare Pages later (2026-10-25). The switch is the `website` job's last three steps, replaced by a Wrangler direct upload from the same job, so the site keeps showing the published version, plus dropping `site/CNAME` |

Name findings of 2026-10-25: the GitHub organization `repolore` and the npm organization `repolore` belong to others (both empty), as does the GitHub organization `repolore-dev`. The maintainer created `repolore-org` on GitHub and npm and requested the PyPI organization `repolore`. Publishing is what holds a name on PyPI and npm; neither reserves one.

## Native build

- `RepoLoreNative=true` turns the CLI project into a NativeAOT build (`PublishAot`, `InvariantGlobalization`, assembly name `repolore`). The code uses only ordinal and invariant string operations, so invariant globalization changes nothing visible; the scenario suite confirms it. A macOS arm64 executable is about 2.2 MB and needs macOS 12 or later.
- Native restores use their own lock files, `packages.native.lock.json`, which pin the SDK's compiler packages (`Microsoft.DotNet.ILCompiler`, `Microsoft.NET.ILLink.Tasks`) and one runtime pack per target. They hold no build-machine entries, so every runner restores them in locked mode.
- The build guard exempts exactly those two compiler packages, only when `PublishAot` is true. `ExecutableFoundationTests.Only_the_native_compiler_packages_are_allowed_and_only_in_a_native_build` checks both modes.
- Each executable is built on a runner of its own platform. Linux builds run on Ubuntu 22.04 so the executables need only glibc 2.35-era symbols; the wheel's `manylinux` tag is computed from the newest glibc symbol the executable uses, and the macOS tag from its minimum OS version. musl (Alpine) is not built.
- The executables link the operating system's networking libraries (on macOS: Network, Security, GSS) because the .NET runtime's native layer does. RepoLore's code cannot call them (build guard); the Linux x64 executable is smoke-tested with networking blocked.

## Packaging and the publish workflow

- `build/distribution.py` (standard library only) builds the archives, wheels and npm packages from an executable, then writes the release files: `repolore-<version>.sha256` (exactly the GitHub release attachments), `repolore-<version>.packages.sha256` (wheels and npm tarballs), the native SBOM `repolore-<version>.native.cdx.json`, the Homebrew formula and the winget manifests. Text files are written with LF, since a CRLF shebang would break the npm launcher.
- `.github/actions/native` builds, smoke-tests (`build/smoke-native.sh`), runs the scenarios against (`REPOLORE_SCENARIO_EXECUTABLE`), packages, and installs one executable through pip, npm and the website script. `ci` runs it for linux-x64, win-x64 and osx-arm64 on every pull request; `publish` runs it for all six platforms.
- `publish` runs only by hand. From a tag `v<version>` it publishes the ticked channels; from a branch it is a dry run. Pushing a tag publishes nothing. Homebrew, winget and the website take stable versions only, and npm publishes a prerelease under the `next` tag. Each publish job checks the checksums before uploading, and re-running skips what already exists.

## One-time setup before the first publish

- GitHub environments `pypi` and `npm` (with the maintainer as required reviewer, like `nuget-preview`), and `github-pages` for the website; Pages source set to GitHub Actions with the custom domain repolore.org and its DNS records.
- PyPI: a pending Trusted Publisher for `repolore` (owner `R-Elias`, repository `repolore`, workflow `publish.yml`, environment `pypi`).
- npm: Trusted Publishing for `repolore-cli` and the six platform packages. npm configures it per existing package, so the first publish probably needs a short-lived token in the `NPM_TOKEN` secret, removed afterwards.
- Homebrew: the repository `repolore-org/homebrew-tap` and a `HOMEBREW_TAP_TOKEN` secret with write access to it.
- winget: a `WINGET_TOKEN` secret (a classic token with `public_repo`) whose account forks `microsoft/winget-pkgs`; the first pull request asks for Microsoft's contributor agreement.
- These two tokens are the only long-lived credentials; NuGet, PyPI and npm publish without stored tokens.

## Limits

- No code signature: macOS blocks a browser download until allowed (notarization needs an Apple Developer ID), and Windows SmartScreen may warn. Package managers and the install scripts are not affected.
- Not yet proven on GitHub: the arm64 and Intel runners, the Windows arm64 toolchain, npm's first publish, `wingetcreate submit`, and the Pages deployment. Local evidence covers macOS arm64 only.
