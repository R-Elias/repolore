# v1.0.0 release plan — package 13

Status: **preparation only; nothing here is a release.** Gates: [implementation guide, section 13](implementation-v1.md#13--release-v100-with-verifiable-artifacts). Depends on [dogfood and preview](dogfood-and-preview.md) (package 12, complete) and [packaging and installation](packaging-and-installation.md). The preview `0.1.0-preview.1` is already published without an author signature.

**Decision, 2026-10-24 (maintainer): author signing is deferred for v1.0.0.** An author certificate costs money and recurring renewal effort, Azure's signing service is not available to an individual in France, and the free route (SignPath Foundation) was not pursued. NuGet clients accept packages without an author signature by default (`signatureValidationMode` is `accept`), and a client in strict `require` mode can trust nuget.org's repository signature, optionally restricted to the package owner. v1.0.0 is therefore made verifiable through Trusted Publishing (no long-lived key), the nuget.org repository signature, a recorded SHA-256 of the exact pushed file, an SBOM, and optionally a GitHub build attestation. The release is labeled "no author signature" and is never described as enterprise-ready, certified, or approved. Revisit only if a concrete adopter requires an author signature; certificate registration on nuget.org is reversible and nothing here blocks adding it in a later version.

## Deliverables and state

| Guide deliverable | State |
|---|---|
| Public license | **Partly done**: `LICENSE` is the Apache-2.0 text and the package declares `Apache-2.0`; the file names **no copyright holder** — needs the maintainer's name and year |
| Release notes | **Not written**; the preview's GitHub release text is the starting point |
| Support/runtime matrix | **Not written**; known facts: .NET 10 SDK to install, 10.0.x runtime to run (`LatestPatch`), CI on `windows-latest`, `ubuntu-latest`, `macos-latest`, networking-blocked run on Linux only |
| Vulnerability-reporting route | **Blocked on a maintainer decision** (GitHub private vulnerability reporting, which must be enabled in repository settings, or an email address) |
| SBOM | **Blocked on a format/tool decision**; the shipped package has no third-party dependencies, so the content is the three project assemblies plus the .NET runtime requirement |
| Verification procedure | **Defined by the decision above**: Trusted Publishing, repository signature checked with `dotnet nuget verify` on the downloaded package, recorded SHA-256 of the pushed file, optional GitHub build attestation (pricing for public repositories not confirmed). Author signing: **deferred** |
| Checksum | **Mechanism exists**: `publish.yml` records `sha256sum` of the exact pushed file; attach it to the GitHub release |
| Single tool artifact; tag equals embedded version | **Done** in `publish.yml` (tag-equals-version check, one `.nupkg`) |
| Network-call trace of the smoke path | **Undecided**: implement, or state the limit (static guards plus the Linux networking-blocked run) in the release notes |
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

## Decisions still needed from the maintainer

1. **Vulnerability reporting:** GitHub private reporting (enable it in repository settings) or an email address; then `SECURITY.md`.
2. **Copyright holder and year** for `LICENSE`/`NOTICE`.
3. **SBOM:** format and tool (a build-time tool is allowed; the shipped CLI must stay dependency-free).
4. **Network-call trace:** implement it or document the limit.
5. **GitHub build attestation:** adopt it or not (confirm that it is free for public repositories first).
6. **When 1.0.0:** after the preview has been used on real work and the issues found are fixed.

Decided: author signing is deferred (2026-10-24), see the top of this note.

## Cleanups done in the package-13 branch (no release behavior)

`publish.yml` and `ci.yml` use Node 24-native action majors (`checkout` v5, `setup-dotnet` v5, `upload-artifact` v6, `download-artifact` v7, chosen as the smallest jump from v4 after reading each action's `action.yml`) and `publish.yml` runs on `ubuntu-24.04`; `ci.yml` no longer runs on every branch push (see below); the obsolete `_repolore/tree/` ignore line is gone; the readme says "no author signature" and the pinning test matches. These take effect for the next tag and for manual runs on `main`; the published `0.1.0-preview.1` keeps its original readme text.

CI cost: `ci.yml` now triggers on pull requests and on pushes to `main` only, cancels superseded pull-request runs, and ignores changes that touch only `_repolore/**`, `docs/**`, `AGENTS.md` or the root `method.md` (no test reads them). A code pull request costs one run per push plus one run on `main` after the merge; a knowledge-only pull request costs none. A branch pushed without a pull request runs nothing. The `publish` workflow is unaffected.
