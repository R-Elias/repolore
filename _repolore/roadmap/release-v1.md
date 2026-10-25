# v1.0.0 release plan — package 13

Status: **preparation only; nothing here is a release.** Gates: [implementation guide, section 13](implementation-v1.md#13--release-v100-with-verifiable-artifacts). Depends on [dogfood and preview](dogfood-and-preview.md) (package 12, complete) and [packaging and installation](packaging-and-installation.md). The preview `0.1.0-preview.1` is already published without an author signature.

**Decision, 2026-10-24 (maintainer): author signing is deferred for v1.0.0.** An author certificate costs money and recurring renewal effort, Azure's signing service is not available to an individual in France, and the free route (SignPath Foundation) was not pursued. NuGet clients accept packages without an author signature by default (`signatureValidationMode` is `accept`), and a client in strict `require` mode can trust nuget.org's repository signature, optionally restricted to the package owner. v1.0.0 is therefore made verifiable through Trusted Publishing (no long-lived key), the nuget.org repository signature, a recorded SHA-256 of the exact pushed file, an SBOM, and optionally a GitHub build attestation. The release is labeled "no author signature" and is never described as enterprise-ready, certified, or approved. Revisit only if a concrete adopter requires an author signature; certificate registration on nuget.org is reversible and nothing here blocks adding it in a later version.

## Deliverables and state

| Guide deliverable | State |
|---|---|
| Public license | **Done**: `LICENSE` is the unmodified Apache-2.0 text; the package declares `Apache-2.0` and carries `Copyright 2026 Elias Aliche` (`<Copyright>`, pinned by the metadata test); the readme names the holder |
| Release notes | **Done for the preview** (`docs/release-notes/0.1.0-preview.1.md`) and drafted for the next version (`docs/release-notes/unreleased.md`, renamed to `<version>.md` at release). `publish.yml` refuses a version without `docs/release-notes/<version>.md` |
| Support/runtime matrix | **Done**: `docs/user-guide.md` section 8 (SDK/runtime, tested operating systems, language-agnostic, evidence for the no-network claim) |
| Vulnerability-reporting route | **Written** (`SECURITY.md`: GitHub private vulnerability reporting, best-effort, scope). **Enabled** by the maintainer (reported on 2026-10-24) |
| SBOM | **Done and run in CI.** `publish.yml` installs CycloneDX 6.3.1 (pinned), calls it by its exact case-sensitive command `dotnet-CycloneDX`, and writes `RepoLore.Cli.<version>.cdx.json` (CycloneDX 1.7 JSON). The first dry run on 2026-10-24 exited 127: the command was called `dotnet-cyclonedx`, which only works on a case-insensitive file system such as macOS. Fixed in PR #8. The SBOM describes application `RepoLore.Cli` depending on the two bundled projects `RepoLore.Core` and `RepoLore.Infrastructure`, with no third-party components and no restore (`--disable-package-restore`). The .NET runtime requirement is documented in the guide, not listed as a component |
| Verification procedure | **Defined by the decision above**: Trusted Publishing, repository signature checked with `dotnet nuget verify` on the downloaded package, recorded SHA-256 of the pushed file, optional GitHub build attestation (pricing for public repositories not confirmed). Author signing: **deferred** |
| Checksum | **Done in the workflow**: one file `RepoLore.Cli.<version>.sha256` covers the exact `.nupkg` and the SBOM; the publish job re-checks it before pushing; the build job writes it to the run summary. Attaching the files to the GitHub release is a manual step |
| Single tool artifact; tag equals embedded version | **Done** in `publish.yml` (tag-equals-version check, one `.nupkg`) |
| Network-call trace | **Dropped by decision (see below)**; the limit is stated in the release notes and the guide |
| Final-candidate smoke on the exact file | **Mechanism exists** (`build/smoke-exact-package.sh`, run by `publish.yml` on the file that is pushed) |

## Author signing — research kept in case it is revisited

Established from Microsoft's [signing documentation](https://learn.microsoft.com/en-us/nuget/create-packages/sign-a-package) (read 2026-10-24):

- A certificate for a public feed must come from a public certificate authority; the page lists Certum (with an open-source offer), Comodo, DigiCert, GlobalSign and SSL.com. **NuGet.org does not accept packages signed with self-issued certificates.**
- `dotnet nuget sign <nupkg> --certificate-path <cer or pfx> | --certificate-fingerprint <sha-2 thumbprint> --timestamper <url>` (.NET 6.0.100 SDK or later). A timestamp is needed so the signature stays valid after the certificate expires; without it signing warns (`NU3002`).
- The certificate must be **registered on NuGet.org** (Account settings, Certificates, as a `.cer` in binary DER form). **After one is registered, every package submission from that account must be signed with a registered certificate.** Removing a certificate makes new submissions signed with it fail; existing packages are unaffected.
- `nuget verify` / `dotnet nuget verify` show signature details; NuGet.org also repository-signs every upload, so the downloaded file differs in bytes from the signed candidate (record the hash of the file that was pushed).

Not established (verify before relying on it):

- Where the private key can live for an automated run. Code-signing keys at public CAs are commonly issued on hardware tokens or in cloud key services, which a GitHub-hosted runner cannot plug in. That would mean signing locally on the maintainer's machine, or a cloud signing service. Check with the chosen CA before buying anything.
- A third-party tool (`SignUniversal`) describes signing NuGet packages through Azure Trusted Signing or Key Vault from any OS; this comes from its own README, not from Microsoft's documentation, and it would be build tooling that holds credentials. No Microsoft statement about signing NuGet packages with Azure's signing service was confirmed.
- How a signing step interacts with Trusted Publishing: the OIDC policy only supplies the push key, so a signed package can still be pushed by `publish.yml`, but the signing credential still needs a safe home outside the source tree.

If author signing is ever revisited, the order of work is as follows. while any certificate is registered on the NuGet.org account, every submission, including a preview, must be signed with a registered one. Registration is reversible (removing the certificate lifts the requirement), but publishing is blocked in between, so register only when the signing step is ready and tested on a throwaway package first.

## Decisions and remaining actions

Decided on 2026-10-24: author signing deferred (top of this note); vulnerability reporting through GitHub private reporting; copyright holder `Elias Aliche`, 2026; SBOM with CycloneDX; **network-call trace dropped** (a trace needs privileged, platform-specific tooling and a single traced run would still not prove zero networking, so the evidence stays the build-time API ban plus the Linux networking-blocked run, stated plainly); **build attestation deferred**: GitHub's attestation pages I read do not state plan or pricing for public repositories, so it is not added, and note that an attestation would cover the pushed file whose bytes differ from the copy NuGet.org serves after adding its repository signature.

Remaining:

1. **Done:** the maintainer enabled private vulnerability reporting and created the GitHub pre-release for `v0.1.0-preview.1` (reported on 2026-10-24).
2. **Done:** `publish` dry run #4 on `main` (`57969d7`, 2026-10-24) was green. Every build step succeeded on GitHub, including the release-notes gate, all four test steps, the pack, the exact-package smoke, the SBOM and the checksum file. The tag check and the publish job were skipped, as they should be outside a tag. The `package` artifact (126406 bytes) was uploaded. Dry run #3 had failed on the SBOM command's case (see the SBOM row).
3. **Scenario tests (package 14a–14b) are complete; run #3 on `main` (`7491ee7`, with the pin at `s41`) was green on all three operating systems. They must be green again** on the release commit ([scenario tests](scenario-tests.md)).
4. **Release candidate `1.0.0-rc.1` (maintainer decision, 2026-10-24).** The published preview predates the fixes and the ignore-file rename, so real use must happen on a candidate that matches what 1.0.0 will ship. Branch `release/1.0.0-rc.1`:
   - `<Version>` is `1.0.0-rc.1`, and the lock files were regenerated; only the project version changed.
   - `docs/release-notes/1.0.0-rc.1.md` lists the changes since the preview. `unreleased.md` was reset for changes after the candidate.
   - The readme and the guide carry a "release candidate" label. The readme test now checks the signature limitation, not the word "Preview".
   - The version test reads `TestSupport.SourceVersion` from `Directory.Build.props` instead of a literal.

   Local checks: Core 101, Infrastructure 81, Cli 150 excluding Packaging and Scenario, Packaging 18, scenarios 27 of 27, the release-notes gate, and `build/smoke-exact-package.sh` on the packed `RepoLore.Cli.1.0.0-rc.1.nupkg`.

   After the merge:
   1. **Done:** `v1.0.0-rc.1` was tagged on `6a8da84` (PR #10). `publish` run #6 was green: the build job, then the approved publish job with OIDC login and push, completed at 20:11:58 UTC on 2026-10-24. NuGet.org showed the version as prerelease and "Validating" right after (maintainer screenshot). The packed candidate built from the same commit is 126163 bytes: three assemblies, their `.pdb` files and the readme, with no dependencies. **Download verified at about 20:18 UTC.** The registration feed lagged the "Listed" page by a few minutes; the first install attempt in that gap got "version not found". The check:
      - A clean `dotnet tool install RepoLore.Cli --version 1.0.0-rc.1 --local`, with only nuget.org as source and empty caches, succeeded.
      - `version` reported `RepoLore 1.0.0-rc.1`, `init` created the knowledge base with checkpoint 1, and `health-check` was healthy.
      - The written `method.md` has hash `393afc14…` with no CR, so the line-ending fix shipped.
      - `dotnet nuget verify --all` exited 0. The only signature is NuGet.org's repository signature, as expected with no author signature. The downloaded file is 139432 bytes including that signature.
      - In this checkout, `dotnet tool restore` with the new pin worked, and the candidate read the history written by the preview: `checkpoint` reported no changes, and `health-check`, which verifies every manifest and object, was healthy.
   2. **Done (2026-10-24):** the GitHub pre-release "RepoLore.Cli 1.0.0-rc.1 (release candidate)" for tag `v1.0.0-rc.1` carries the release notes and three files from run #6's `package` artifact:
      - `RepoLore.Cli.1.0.0-rc.1.nupkg`, the exact pushed file without NuGet.org's signature;
      - `.cdx.json`;
      - `.sha256`.

      Downloaded from the release, `shasum -a 256 -c` reported OK for both the package (`cc9cb133…`) and the SBOM (`9ba3f566…`). The SBOM is CycloneDX 1.7 for `RepoLore.Cli 1.0.0-rc.1`, with components `RepoLore.Core` and `RepoLore.Infrastructure`. Attaching the `.nupkg` is what lets anyone use the checksum, since NuGet.org serves a re-signed copy with different bytes.
   3. Branch `chore/pin-1.0.0-rc.1` moves `.config/dotnet-tools.json` to `1.0.0-rc.1`. It also carries two edits that missed the PR #10 commits: the AGENTS.md paragraph on the removed alpha tools, and the `CheckoutIgnoreRulesTests` example path.
5. **Use the candidate on real work** and fix what it shows (as `1.0.0-rc.2` if a fix is needed).
   - **Field test (2026-10-24):** an autonomous agent, prompted by the maintainer, built a Python playground repository beside this checkout (`repolore-field-test/`, outside this repository) and ran 57 journaled experiments on the published `1.0.0-rc.1`. Read-only commands never wrote, and recovery survived interrupted writes. Verified against the code and contract on 2026-10-25: 4 real bugs (no high severity), 4 of the agent's 8 bug claims were not bugs (fail-safe design, documented link limits, macOS case-insensitivity), and 16 documentation gaps. One claimed gap did not reproduce: a leftover `_repoloreignore` does not disable `--explain`.
   - **Fixes, branch `fix/field-test-findings`:** the undo checkpoint named by `restore`, `migrate` and `init --update-method`; nested `init` refused; `--explain` with disabled history; percent-encoded links; `--quiet` for `path` and `tree`; remedy text for corrupt history; the method's context guidance; and the user-guide gaps ([implementation record](implementation-v1.md), `docs/release-notes/unreleased.md`). Not taken: the friction items (wishes such as directory restore, `-h`, per-command help, warnings for unknown `repolore.json` keys), which stay candidates for after 1.0.0.
   - PR #13 merged both commits (`e2fc740`); `ci` was green on all three operating systems, now including the scenarios (reported by the maintainer, 2026-10-25).
   - **Release candidate `1.0.0-rc.2`, branch `release/1.0.0-rc.2`:** `<Version>` is `1.0.0-rc.2` and the lock files were regenerated (only the project version changed). `docs/release-notes/1.0.0-rc.2.md` holds the field-test entries from `unreleased.md`, which was reset; the readme and the guide name `1.0.0-rc.2`. The tool pin stays at `1.0.0-rc.1` until rc.2 is published.
   - **Published (2026-10-25):** `publish` dry run #7 on `main` (`e7210e8`, PR #14) was green. Tag `v1.0.0-rc.2` (lightweight, like rc.1) on that commit started run #8: build green, and the publish job, approved by the maintainer, completed at 11:12:23 UTC.
     - A clean `dotnet tool install RepoLore.Cli --version 1.0.0-rc.2 --local`, with only nuget.org as source and empty caches, succeeded. `version` reported `RepoLore 1.0.0-rc.2`, `init` created a knowledge base, `health-check` was healthy, and `init` in a subdirectory of it exited 2.
     - The written `method.md` is byte-identical to this repository's, with no CR.
     - `dotnet nuget verify --all` exited 0 with only NuGet.org's repository signature. The downloaded file is 140781 bytes with that signature; the pushed file is 127691 bytes.
     - The GitHub pre-release "RepoLore.Cli 1.0.0-rc.2 (release candidate)" carries the `.nupkg`, `.cdx.json` and `.sha256` from run #8. Downloaded from the release, `shasum -a 256 -c` reported OK for the package (`7291e79c…`) and the SBOM (`ff3601dc…`). The SBOM is CycloneDX 1.7 for `RepoLore.Cli 1.0.0-rc.2`, with components `RepoLore.Core` and `RepoLore.Infrastructure`.
     - Branch `chore/pin-1.0.0-rc.2` moves `.config/dotnet-tools.json` to `1.0.0-rc.2`. In this checkout the candidate listed the 41 existing checkpoints, took checkpoint 42 and was healthy, and `init --update-method` had nothing to do: the method copies already match what it ships.
   - **Contract review (2026-10-25):** before the cut, the field-test friction list was sorted into "decide before 1.0" (would change results scripts rely on) and "safe after 1.0" (additive, or affecting only damaged states). Maintainer decisions:
     - **Fixed:** `init --update-method --json` `checkpointId`; six-digit checkpoint times; `tree` order and content; trailing whitespace in rules refused; `migrate --check --dry-run` refused. Branch `fix/pre-1.0-contract`.
     - **Frozen as is:** the budget order; unknown `repolore.json` keys ignored (newer 1.x keys must not fail older tools, and misspellings fail toward more protection); exit 3 for a `restore --path` outside coverage; `migrate` JSON `ambiguous` versus text `conflict`; no `.gitignore` check (needs Git); disabled history or a too-small limit blocking `restore`.
     - **After 1.0:** `-h`, `--version`, per-command help, directory restore, preview diffs, per-checkpoint changes in `history`, restored paths in JSON, `./` and absolute targets, source paths for `tree --start`, new findings for real defects (conflict markers, a pending operation in `context`, missing or mismatched manifests, leftover alpha `tree/`, invalid session names, links into `.history/`), and wording fixes.
     - Scenarios tagged `contract` pin both the fixed and the frozen items ([scenario tests](scenario-tests.md)).
   - PR #16 merged them (`f555624`) after the maintainer pushed `s43`–`s46`; `ci` was green on all three operating systems, scenarios included (reported 2026-10-25).
   - **Release candidate `1.0.0-rc.3`, branch `release/1.0.0-rc.3`:** `<Version>` is `1.0.0-rc.3`, the lock files were regenerated (only the project version changed), `docs/release-notes/1.0.0-rc.3.md` holds the contract entries from `unreleased.md` (reset), and the readme and the guide name `1.0.0-rc.3`. Its upgrade note warns about rules ending with a space. The tool pin stays at `1.0.0-rc.2` until rc.3 is published.
   - **Published (2026-10-25):** `publish` dry run #9 on `main` (`2df6477`, PR #17) was green. Tag `v1.0.0-rc.3` (lightweight) on that commit started run #10: build green, and the publish job, approved by the maintainer, completed at 12:27:41 UTC. NuGet.org's flat container listed it a few minutes later and the registration index (which `dotnet tool install` resolves) about 80 seconds after that; an install in between fails with "version not found".
     - A clean install from nuget.org only, with empty caches, succeeded. `version` reported `RepoLore 1.0.0-rc.3`; `init` and `health-check` worked; the written method is byte-identical to this repository's; `history --json` showed a six-digit time; `tree` listed only the two notes; a `.repoloreignore` rule ending with a space gave the new `policy-error` (exit 3).
     - `dotnet nuget verify --all` exited 0 with only NuGet.org's repository signature. The downloaded file is 141440 bytes with that signature; the pushed file is 128351 bytes.
     - The GitHub pre-release "RepoLore.Cli 1.0.0-rc.3 (release candidate)" carries the `.nupkg`, `.cdx.json` and `.sha256` from run #10. Downloaded from the release, `shasum -a 256 -c` reported OK for the package (`b5929245…`) and the SBOM (`99a5deee…`); the SBOM is CycloneDX 1.7 for `RepoLore.Cli 1.0.0-rc.3` with `RepoLore.Core` and `RepoLore.Infrastructure`.
     - Branch `chore/pin-1.0.0-rc.3` moves the pin. Here the candidate read the 45 existing checkpoints (the newest, written by the source build, already with six digits), took checkpoint 46 and was healthy, and `init --update-method` had nothing to do.
   - PR #18 moved the pin to `1.0.0-rc.3` (`d7df583`).
   - **Cut without a further use period (maintainer decision, 2026-10-25):** the field test already exercised the candidates harder than ordinary use would, and rc.3 changed only contract items that the `contract` scenarios pin.
6. **The cut** (branch `release/1.0.0`, prepared 2026-10-25):
   - **Done on the branch:** `<Version>` is `1.0.0`, and the lock files were regenerated (only the project version changed). `docs/release-notes/1.0.0.md` summarizes the changes since the preview, links the three candidates' notes, gives upgrade steps and states what stays stable in 1.x (exit codes, IDs, JSON fields, options, format 1 and its history, the `contract` scenarios); `unreleased.md` was reset for changes after 1.0.0. The readme and the guide lost their release-candidate labels but keep the no-author-signature statement, which the package-readme test checks. `SECURITY.md` says the preview and the candidates get no fixes of their own. The `publish` environment keeps its name `nuget-preview`, which is a GitHub setting.
   - `scenarios` run in `ci` on the merge commit, and the publish dry run repeats every test step;
   - tag `v1.0.0` and approve the publish job;
   - verify the download from NuGet.org;
   - create the GitHub release (not a pre-release) with the package, the checksum file and the SBOM attached;
   - move the pin to `1.0.0`, and mark package 13 complete in the [implementation record](implementation-v1.md).

   **Published (2026-10-25):** PR #19 merged the cut (`b1cec96`); `ci` was green on the PR head, whose tree is identical, and on the merge commit. `publish` dry run #11 on `b1cec96` was green. The maintainer tagged `v1.0.0` (lightweight) on that commit, which started run #12: build green, and the approved publish job completed at 12:55:27 UTC. NuGet.org's website showed it first; the registration index that `dotnet tool install` resolves served it about 8 minutes after the push.
   - A clean install from nuget.org only, with empty caches, succeeded. `version` reported `RepoLore 1.0.0`; `init` and `health-check` worked; the written method is byte-identical to this repository's; `history --json` showed a six-digit time; `tree` listed only the notes; `init` below the new knowledge base exited 2.
   - `dotnet nuget verify --all` exited 0 with only NuGet.org's repository signature. The downloaded file is 141340 bytes with that signature; the pushed file is 128251 bytes.
   - The GitHub release "RepoLore.Cli 1.0.0" is a full release and the repository's latest. It carries the `.nupkg`, `.cdx.json` and `.sha256` from run #12; downloaded from the release, `shasum -a 256 -c` reported OK for both files, and the SBOM is CycloneDX 1.7 for `RepoLore.Cli 1.0.0` with `RepoLore.Core` and `RepoLore.Infrastructure`.
   - Branch `chore/pin-1.0.0` moves the pin. Here 1.0.0 read the 48 existing checkpoints, `checkpoint` had no changes, `health-check` was healthy, and `init --update-method` had nothing to do.
   - Package 13 is complete. After 1.0.0: package 14c and the post-1.0 list from the contract review.

## Cleanups done in the package-13 branch (no release behavior)

`publish.yml` and `ci.yml` use Node 24-native action majors (`checkout` v5, `setup-dotnet` v5, `upload-artifact` v6, `download-artifact` v7, chosen as the smallest jump from v4 after reading each action's `action.yml`) and `publish.yml` runs on `ubuntu-24.04`; `ci.yml` no longer runs on every branch push (see below); the obsolete `_repolore/tree/` ignore line is gone; the readme says "no author signature" and the pinning test matches. These take effect for the next tag and for manual runs on `main`; the published `0.1.0-preview.1` keeps its original readme text.

CI cost: `ci.yml` now triggers on pull requests and on pushes to `main` only, cancels superseded pull-request runs, and ignores changes that touch only `_repolore/**`, `docs/**`, `AGENTS.md` or the root `method.md` (no test reads them). A code pull request costs one run per push plus one run on `main` after the merge; a knowledge-only pull request costs none. A branch pushed without a pull request runs nothing. The `publish` workflow is unaffected.
