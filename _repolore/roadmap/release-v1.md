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
   1. Tag `v1.0.0-rc.1` on `main`, approve the publish job, and verify the download as for the preview.
   2. Attach the checksum file and SBOM to a GitHub pre-release.
   3. Then, on a branch, move `.config/dotnet-tools.json` to `1.0.0-rc.1`, which cannot happen before the package exists.
5. **Use the candidate on real work** and fix what it shows (as `1.0.0-rc.2` if a fix is needed).
6. **The cut:**
   - bump `<Version>` to `1.0.0` and write `docs/release-notes/1.0.0.md` from the candidate's notes plus `unreleased.md`;
   - run `scenarios` on that commit;
   - tag `v1.0.0` and approve the publish job;
   - verify the download from NuGet.org;
   - create the GitHub release with the checksum file and SBOM attached.

## Cleanups done in the package-13 branch (no release behavior)

`publish.yml` and `ci.yml` use Node 24-native action majors (`checkout` v5, `setup-dotnet` v5, `upload-artifact` v6, `download-artifact` v7, chosen as the smallest jump from v4 after reading each action's `action.yml`) and `publish.yml` runs on `ubuntu-24.04`; `ci.yml` no longer runs on every branch push (see below); the obsolete `_repolore/tree/` ignore line is gone; the readme says "no author signature" and the pinning test matches. These take effect for the next tag and for manual runs on `main`; the published `0.1.0-preview.1` keeps its original readme text.

CI cost: `ci.yml` now triggers on pull requests and on pushes to `main` only, cancels superseded pull-request runs, and ignores changes that touch only `_repolore/**`, `docs/**`, `AGENTS.md` or the root `method.md` (no test reads them). A code pull request costs one run per push plus one run on `main` after the merge; a knowledge-only pull request costs none. A branch pushed without a pull request runs nothing. The `publish` workflow is unaffected.
