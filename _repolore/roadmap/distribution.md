# Distribution beyond NuGet

Status: **set up in PR #21; `1.1.0` (PR #22, tag `v1.1.0`, publish run #14) published on 2026-10-25 to every channel except winget, whose first pull request awaits Microsoft's review; `1.1.1` (NuGet ID `RepoLore`) followed the same evening.** The dry run #13 from the setup branch was green on all six platforms.

1.1.0, verified on 2026-10-25 by downloading or installing on macOS arm64 (other platforms: their files are byte-identical to the ones the run tested on their own runners):

- NuGet: `RepoLore.Cli` 1.1.0 installs as a local tool and runs; repository signature only. The `RepoLore` ID was not published.
- GitHub release "RepoLore 1.1.0": 10 assets, all matching `repolore-1.1.0.sha256`, itself identical to the run's.
- PyPI: the six wheels' digests equal `repolore-1.1.0.packages.sha256`; the macOS arm64 wheel installs a running executable. The Linux tag is `manylinux_2_34`.
- npm: the run's npm job failed (see the `./` note below), so the maintainer published the run's checksum-verified tarballs from the maintainer's machine with `build/npm-publish.sh`, after enabling 2FA (a passkey; npm requires 2FA to publish). The registry's seven tarballs equal those files; these versions carry no provenance attestation. New packages show a `0.0.0-stage` placeholder for a few minutes while npm processes them. Trusted publishing (`R-Elias/repolore`, `publish.yml`, environment `npm`) was then set on all seven with `npm trust github`, so `NPM_TOKEN` is to be removed.
- Homebrew: the tap's formula equals the run's, and its URLs and checksums match the release.
- Website: the first deployment was refused because `github-pages` allowed only `main`; with the tag rule `v*` added, a re-run of the job deployed it, and `install.sh` installs a running 1.1.0.
- This checkout pinned `1.1.0` from branch `chore/pin-1.1.0` until 1.1.1: it read the 59 existing checkpoints, had nothing to checkpoint, was healthy, and the method is unchanged since `1.0.0-rc.2`.
- winget: pull request microsoft/winget-pkgs#461674. Its URL check failed because the homepage was still 404 at the time; after the CLA comment, an empty commit pushed to the pull request's branch restarted validation (`@wingetbot run` is documented among the moderators' tools).

1.1.1 (PR #25, tag `v1.1.1`), published on 2026-10-25 with every channel ticked except winget (its 1.1.0 pull request was still in review and the executable was unchanged); its only change is the NuGet package ID `RepoLore`. Verified the same way: the release's 9 checksums, PyPI's 6 wheels, npm's 7 packages (now with SLSA provenance attestations, so trusted publishing works without a token), the Homebrew formula's URLs and checksums, the website and `install.sh`, and `RepoLore` 1.1.1 installed from NuGet.org with its repository signature. The first NuGet push was refused (403) because the trusted publishing policy's package pattern did not cover the new ID; after the maintainer widened it, a re-run of the job published it. NuGet.org served the new ID about 5 minutes after the push. This checkout then moved its manifest from `RepoLore.Cli` 1.1.0 to `RepoLore` 1.1.1 (branch `chore/pin-repolore-1.1.1`) with `dotnet tool uninstall RepoLore.Cli --local` and `dotnet tool install RepoLore --version 1.1.1 --local`; it read the 61 existing checkpoints, had nothing to checkpoint and was healthy. Deprecating every `RepoLore.Cli` version (Legacy, alternate `RepoLore`) and the `RepoLore` prefix reservation are manual steps on nuget.org.

A Defender detection blocks the winget pull request: its installation test (2026-10-25) ended with `Validation-Defender-Error` and `Validation-Executable-Error`, although both installs completed and the manifest's `RelativeFilePath` matches the archives. The likely cause is a false positive on a new, unsigned NativeAOT executable, which then disappears so the test cannot find it; the maintainer is to submit the executables to Microsoft for analysis and answer on the pull request within 10 days. Any Windows channel can meet the same detection until Microsoft clears it; code signing would be the lasting fix.

## Decision (maintainer, 2026-10-25)

Not everyone has or wants .NET, so the same tool also ships as a native executable, wrapped for the package managers that companies and developers already use. This replaced the v1 boundary "one NuGet package, no additional package ecosystems". What stays: one CLI, one code base, one tagged commit and one publish workflow for every channel; no separate Core package, plugins or hosted service.

## Channels and names

| Channel | Name | Notes |
|---|---|---|
| NuGet | `RepoLore` (`RepoLore.Cli` up to 1.1.0) | Renamed in 1.1.1 by the maintainer's decision of 2026-10-25, while few users pinned the old ID; once 1.1.1 is published, every `RepoLore.Cli` version is to be deprecated with `RepoLore` as the alternate (a manual step on nuget.org). A NuGet.org ID stays its owner's for good, so nobody can take `RepoLore.Cli`. Request the `RepoLore` ID prefix reservation with NuGet.org |
| GitHub release | `repolore-<version>-<rid>.zip` / `.tar.gz` | `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`; each archive holds the executable, `LICENSE` and `THIRD-PARTY-NOTICES.txt` |
| PyPI | `repolore` | One wheel per platform, the executable in the wheel's scripts folder: no Python launcher, no Python at run time |
| npm | `repolore-cli`, platform packages `@repolore-org/cli-<platform>-<arch>` | `repolore` on npm is an unrelated, similar project (created 2026-07-18) that also installs a `repolore` command. The launcher starts the executable as a child process: the npm channel's addition, not RepoLore's |
| Homebrew | formula `repolore` in the tap `repolore-org/tap` | `brew install repolore-org/tap/repolore`, or `brew install repolore` once the tap is added. A bare `brew install repolore` without the tap needs Homebrew's main catalogue, which requires a well-known project |
| winget | moniker `repolore` (identifier `RepoLore.RepoLore`) | winget identifiers need a `Publisher.Package` form, so `winget install repolore` works through the manifest's moniker. A zip with a nested portable executable; each version is a pull request to `microsoft/winget-pkgs`, reviewed by Microsoft |
| Website | repolore.org | `site/`: a download page plus `install.sh` and `install.ps1`, which check the archive against the release checksums. Deployed to GitHub Pages for now; the maintainer plans Cloudflare Pages later (2026-10-25). The switch is the `website` job's last three steps, replaced by a Wrangler direct upload from the same job, so the site keeps showing the published version, plus dropping `site/CNAME` |

Name findings of 2026-10-25: the GitHub organization `repolore` and the npm organization `repolore` belong to others (both empty), as does the GitHub organization `repolore-dev`. The maintainer created `repolore-org` on GitHub and npm and requested the PyPI organization `repolore`. Publishing is what holds a name on PyPI and npm; neither reserves one.

## Native build

- `RepoLoreNative=true` turns the CLI project into a NativeAOT build (`PublishAot`, `InvariantGlobalization`, assembly name `repolore`). The code uses only ordinal and invariant string operations, so invariant globalization changes nothing visible; the scenario suite confirms it. A macOS arm64 executable is about 2.2 MB and needs macOS 12 or later.
- Native restores use their own lock files, `packages.native.lock.json`, which pin the SDK's compiler packages (`Microsoft.DotNet.ILCompiler`, `Microsoft.NET.ILLink.Tasks`) and one runtime pack per target. They hold no build-machine entries, so every runner restores them in locked mode. A native build targets the SDK's own platform unless `RuntimeIdentifier` is given, because .NET 10 puts the NativeAOT framework in a separate runtime pack (`Microsoft.NETCore.App.Runtime.NativeAOT.<rid>`) that restore downloads only for a known target; without it, publish fails with "The PrivateSdkAssemblies ItemGroup is required" on a machine whose package cache lacks the pack (found in the first CI run, 2026-10-25). Passing the platform to restore as a global property instead breaks locked mode for Core and Infrastructure.
- The build guard exempts exactly those two compiler packages, only when `PublishAot` is true. `ExecutableFoundationTests.Only_the_native_compiler_packages_are_allowed_and_only_in_a_native_build` checks both modes.
- Each executable is built on a runner of its own platform. Linux builds run on Ubuntu 22.04 so the executables need only glibc 2.35-era symbols; the wheel's `manylinux` tag is computed from the newest glibc symbol the executable uses, and the macOS tag from its minimum OS version. musl (Alpine) is not built.
- Output is UTF-8 on every channel: on Windows, `Program.cs` switches the console to UTF-8 at start, as the .NET CLI does before `dotnet tool run` starts a tool. Without that, the executable wrote in the console's code page (437 on the CI runner) and three scenarios with `docs/équipe/` failed on Windows, while the NuGet tool passed only because it ran through `dotnet tool run`. `CommandContractTests.Output_is_utf8_whatever_the_console_code_page` starts the CLI directly and guards it.
- The executables link the operating system's networking libraries (on macOS: Network, Security, GSS) because the .NET runtime's native layer does. RepoLore's code cannot call them (build guard); the Linux x64 executable is smoke-tested with networking blocked.

## Packaging and the publish workflow

- `build/distribution.py` (standard library only) builds the archives, wheels and npm packages from an executable, then writes the release files: `repolore-<version>.sha256` (exactly the GitHub release attachments), `repolore-<version>.packages.sha256` (wheels and npm tarballs), the native SBOM `repolore-<version>.native.cdx.json`, the Homebrew formula and the winget manifests. Text files are written with LF, since a CRLF shebang would break the npm launcher.
- `.github/actions/native` builds, smoke-tests (`build/smoke-native.sh`), runs the scenarios against (`REPOLORE_SCENARIO_EXECUTABLE`), packages, and installs one executable through pip, npm and the website script. `ci` runs it for linux-x64, win-x64 and osx-arm64 on every pull request; `publish` runs it for all six platforms.
- `publish` runs only by hand. From a tag `v<version>` it publishes the ticked channels; from a branch it is a dry run. Pushing a tag publishes nothing. Homebrew, winget and the website take stable versions only, and npm publishes a prerelease under the `next` tag. Each publish job checks the checksums before uploading, and re-running skips what already exists.
- A run uses the workflow file of the ref it runs from, so a fix to `publish.yml` never reaches an existing tag: the 1.1.0 run, whose npm job failed, could not be repaired by re-running it. That job had called `npm publish release/<file>.tgz`, which npm reads as the GitHub repository `release/<file>.tgz`, and CI had only installed the tarballs. `build/npm-publish.sh` now prefixes `./`, and `assemble` runs it with `--dry-run` in every run, dry runs included, so the exact publish command is exercised before a tag.

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
