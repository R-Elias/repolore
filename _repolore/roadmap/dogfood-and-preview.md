# Dogfood and NuGet preview — package 12

Prepares the preview; **nothing is published and this checkout is not migrated**. Gates: [implementation guide](implementation-v1.md#12--exercise-the-complete-workflow-and-publish-a-preview). Depends on [packaging and installation](packaging-and-installation.md) (package 11). Behavior referenced here is in [durable and session context](durable-and-session-context.md), [snapshot capture](snapshot-capture.md), [restore and recovery](restore-and-recovery.md), [initialization](initialization-and-method-updates.md), [alpha migration](alpha-migration.md), and [health and CLI contract](health-and-cli-contract.md).

## What package 12 delivers

| Deliverable | Where |
|---|---|
| Install, internal feed, daily use, sessions, format/migration, recovery instructions | `docs/user-guide.md` (user-facing, outside `_repolore/`) |
| Package readme that states the preview/unsigned label | `README.md`, packed as `PackageReadmeFile` (whitelist test updated deliberately) |
| One scripted acceptance journey on disposable fixtures | `tests/RepoLore.Cli.Tests/Packaging/Journey/AcceptanceJourneyTests.cs` |
| Method and agent guidance reflecting implemented behavior | both `method.md` copies (banner first, then replaced by the shipped v1 method, identical), `AGENTS.md`, `.config/dotnet-tools.json` |
| Dogfood backup procedure, preview checklist, publication workflow and steps | this note; `.github/workflows/publish.yml`, `build/smoke-exact-package.sh` (migration done 2026-10-24; publication prepared, not executed) |

## Acceptance journey

`AcceptanceJourneyTests` runs the documented order against the **real packed `.nupkg`**: a temp consumer repository outside the checkout, a `<clear/>` config with only a temp local feed, an empty NuGet cache, isolated `DOTNET_CLI_HOME`, and Git configuration isolated from the machine (`GIT_CONFIG_GLOBAL` = empty file, `GIT_CONFIG_NOSYSTEM`, `core.autocrlf=false`). Git is invoked only by the test (`GitRepository`); the shipped CLI still spawns nothing. Time and IDs are not asserted: checkpoint IDs are the tool's deterministic counter, session IDs are fixed literals.

| # | Step | Evidence asserted |
|---|---|---|
| 1 | Restore the pinned manifest into an empty cache | exit 0, `version` equals `Directory.Build.props` |
| 2 | `init` | checkpoint 1, `repolore.json` SHA-256 equals the independently computed hash; **control:** `git status` lists `.history/` as untracked until ignored |
| 3 | Apply the exclusions `init --json` recommends | `.gitignore` hash; `.history` and session paths ignored, a durable note is not |
| 4 | Durable note, commit baseline | staged set is exactly the seven expected paths; `health-check` healthy |
| 5 | Session A and B | `git status` empty (ignored); both ignored by `git check-ignore` |
| 6 | `checkpoint` | checkpoint 2 lists both sessions and the durable note |
| 7 | Default `context src/` | exactly root + durable note; durable sentinel; **no** session sentinel |
| 8 | `context src/ --session A` (and `--node`) | adds only A's root (or requested note), labeled `session:<A>`; no B sentinel; **control:** A selecting B's note exits 2 |
| 9 | Edit A, `checkpoint` | checkpoint 3, one file changed |
| 10 | **Control:** durable note linking to the session | `health-check` exit 1 `session-link-in-durable-note` |
| 11 | Promote the verified fact | text has no session id or sentinel; `health-check` healthy; checkpoint 4 |
| 12 | Review the intended diff | `git status` is exactly ` M _repolore/sparse-tree/src/src.md`; the diff has the promoted sentinel and no session id/sentinel; commit |
| 13 | `restore 2 --path <A note>` (dry run, then apply) | dry run leaves hash unchanged; apply gives the draft hash; A root, B root, durable note hashes unchanged; checkpoint 5; the overwritten version is still restorable from checkpoint 4 |
| 14 | Explicit cleanup of A | complete-marker checkpoint 6; folder deleted; checkpoint 7 reports 2 removed; `tree --start _repolore/sessions/` lists only B; dry-run restore from checkpoint 6 still offers A's file |
| 15 | No session/history path in `ls-files` or any commit | exact |
| 16 | Fresh `git clone`, empty cache, committed manifest restored from the feed | no `sessions/` or `.history/` before or after reads; durable hash; `context` shows the promoted fact and no session text; `health-check` healthy; `history` empty; `--session A` is an `invalid-session` finding (exit 1); first `checkpoint` is checkpoint 1 |

Expected file hashes live in `JourneyContent` and were computed independently (Python `hashlib`) from the literal text; `JourneyFile.WriteTo` re-checks the written bytes, so an editor, BOM or line-ending change fails the run. Mutation checks done on 2026-10-23 (then reverted): skipping the ignore entries fails on the `.gitignore` hash; force-adding a session file to Git fails step 15; editing `README.md` fails the readme test.

Related controls: `InitTests.A_session_folder_created_before_init_makes_init_refuse_and_change_nothing` (a `_repolore/` holding only a session folder is alpha to `init`, so the documented order is init first); `CheckoutIgnoreRulesTests` (this checkout's `.gitignore` ignores sessions and history and not durable notes, checked in a throwaway repository, not by running Git in the checkout); the package whitelist and forbidden-name tests keep `sessions`, `.history`, fixtures and the test stack out of the `.nupkg`.

The journey is not a performance or productivity measurement; no numerical improvement is claimed.

## Dogfood migration of this checkout — run 2026-10-24

Rehearsed on 2026-10-23 on a disposable `git clone` of HEAD (never on this checkout): `migrate --check` exit 1; `--dry-run` plans one `create _repolore/repolore.json` and keeps `root.md` and `sparse-tree/tools/tools.md`; `product/` and `roadmap/` are reported as untouched custom areas. After `migrate`: SHA-256 of all 19 files unchanged (`diff` of before/after manifests empty), `repolore.json` is `{"formatVersion":1}`, `health-check` healthy, second `migrate` is a no-op, `restore 1` (the migration baseline) deleted only the marker and `health-check` reported `migration-needed` again.

**Run on 2026-10-24 with the maintainer's go-ahead, using the actual packed `RepoLore.Cli.0.1.0-preview.1.nupkg` installed from a local-only `<clear/>` feed into a pinned manifest kept outside the checkout** (isolated NuGet/`DOTNET_CLI_HOME` caches, `--repo-root` pointing at the checkout; the checkout got no manifest). Results:

- Backup first, outside the repository and `.history/`: `~/repolore-backups/2026-10-24-pre-migration/` holds `repolore-poc.bundle` (`git bundle verify`: complete history), a copy of `_repolore/`, `method.md`, `.gitignore`, and `uncommitted.diff` (two uncommitted completion-record edits, preserved untouched). SHA-256 manifests of the live and backup `_repolore/` (20 files) were identical.
- A fresh-clone rehearsal with the installed package gave the same plan as the real checkout: `migrate --check` exit 1 (candidates `root.md`, `sparse-tree/tools/tools.md`), `--dry-run` exit 0 with one `create _repolore/repolore.json`.
- Applied: `migrated: format marker written, 0 notes copied, 0 legacy files removed (checkpoint 0000000000000002)`; baseline checkpoint 1 is the migration baseline, checkpoint 2 the result. `health-check` healthy, second `migrate` a no-op, `.history/` Gitignored. All 20 pre-existing files are byte-identical to the verified backup state; `repolore.json` is `{"formatVersion":1}`; `git status` shows only the new `repolore.json` plus the two pre-existing uncommitted edits.
- Not done at migration time, deliberately: `init --update-method`, any change to the method bodies (both done later the same day, see below), and removing the obsolete `_repolore/tree/` line from `.gitignore` (still present, harmless). The method banners (both copies, identical) now say the alpha body is superseded for this checkout. The rollback path (`restore 1`, which removes only the marker) was rehearsed on the clone, not run here.

Original procedure (kept as the reference for a repeat on another knowledge base):

1. `git status` — preserve every uncommitted change; commit or stash deliberately, never discard.
2. Outside the repository **and outside `_repolore/.history/`**: `git bundle create <backup-dir>/repolore-poc-<date>.bundle --all`, `cp -R _repolore <backup-dir>/_repolore`, `cp method.md .gitignore <backup-dir>/`.
3. Verify: `git bundle verify <bundle>`; run `find _repolore -type f -print0 | sort -z | xargs -0 shasum -a 256` on the live and backup copies and require an empty `diff`.
4. Install the tool from the local feed in a manifest **outside** this checkout (this checkout deliberately has no manifest yet) and rehearse once more on a fresh clone.
5. In the checkout: `repolore migrate --check`, `--dry-run`; confirm the plan equals the rehearsal.
6. `repolore migrate`; then `health-check`; compare the file manifest (only `repolore.json` added).
7. Decide separately about `init --update-method` (replaces the alpha `_repolore/method.md` with shipped v1 guidance) and about the repository-root `method.md`, which AGENTS.md requires to stay identical to `_repolore/method.md`. Remove the obsolete `_repolore/tree/` line from `.gitignore` only deliberately.
8. To undo: `restore <baseline id>`; if that were ever insufficient, restore from the verified backup.

## Adoption of the published tool and v1 method — 2026-10-24

Done on the branch `chore/adopt-published-tool`, after the preview was verified. A verified backup of both method files is in `~/repolore-backups/2026-10-24-pre-method-update/` (SHA-256 `cf89a3adb66378f8…`, identical for both copies).

- `.config/dotnet-tools.json` was created with `dotnet new tool-manifest` and `dotnet tool install RepoLore.Cli --version 0.1.0-preview.1 --local` (nuget.org through the repository `NuGet.Config`); `dotnet tool run repolore -- version` reports `0.1.0-preview.1`.
- `dotnet tool run repolore -- init --update-method` with that published tool replaced `_repolore/method.md` (376 alpha lines became the shipped 48-line v1 method, SHA-256 `393afc14c09884d5…`, the same object the first `init` stores on any fresh repository). The pre-operation state was already captured (checkpoint 9) and `restore 9 --path _repolore/method.md --dry-run` offers to put the old bytes back. A second run is a no-op.
- The result was copied to the repository-root `method.md`; `cmp` confirms the two copies are identical. `health-check` healthy.
- `AGENTS.md`, `root.md`, the executable-foundation note and the initialization note were updated to match. Dated historical records in the completion record were left as written.
- Left alone on purpose: the PowerShell alpha tools and `_repolore/sparse-tree/tools/tools.md`, the obsolete `_repolore/tree/` line in `.gitignore`.

## Preview readiness

| Item | State |
|---|---|
| Package builds, installs from a clean local-only feed, runs the full journey | **Ready**: macOS arm64 locally, and the maintainer reported the Windows/Linux/macOS CI matrix green after the package-12 push (2026-10-24; run not inspected from here). Package 12.5 is also reported green on all three |
| Contents whitelist, no knowledge/sessions/history/test stack/build paths | **Ready** (readme added deliberately) |
| User documentation and package readme | **Ready**; NuGet.org rendering of the readme unseen until a publish |
| `RepoLore.Cli` ID ownership, `RepoLore.` prefix reservation | **ID done**: `RepoLore.Cli` is published and owned by the nuget.org account `EliasAliche` (first push through the policy created the ID). **Prefix reservation** is optional and not requested |
| `Authors`, `PackageProjectUrl`/`RepositoryUrl` | **Decided**: `Authors` is `Elias Aliche` (package 12.5); the URLs stay `https://github.com/R-Elias/repolore` (equals `origin`; the `repolore` GitHub organization is taken by someone else). The repository is public (checked 2026-10-24) |
| Publish workflow (`publish.yml`) and exact-bytes smoke (`build/smoke-exact-package.sh`) | **Done**: dry run on `main` green (commit `e140967…`), tag run green, package published (see the result below) |
| nuget.org Trusted Publishing policy, GitHub environment `nuget-preview`, secret `NUGET_USER` | **Done and proven by the publish**. The `v*` environment rule had first been created as a *branch* rule and the first publish attempt was refused at the environment gate; the maintainer recreated it as a *tag* rule and the re-run succeeded. Administrator bypass is disabled |
| Package signing | **No author signature**; a preview ships without one; v1.0.0 gate (package 13). The published file carries NuGet.org's repository signature only |
| Network-call trace of the smoke path | **Not implemented**; static guards, the boundary-guard build, and the Linux networking-blocked run are the evidence |
| Linux networking-blocked run of the journey | Runs inside that CI step; covered by the reported green matrix |
| Dogfood migration of this checkout | **Done 2026-10-24** (see the section above); the method-body replacement is still the maintainer's decision |
| SBOM, checksum file, vulnerability-reporting contact, release notes | Package 13 |

**The preview must state:** it is a preview (`0.1.0-preview.1`); the package is unsigned (NuGet.org adds only its own repository signature after upload, which is not an author signature); it is not enterprise-ready, certified, or a guarantee about what external agents or editors do with content they read; it needs a .NET 10 SDK to install and a .NET 10.0.x runtime; the networking-blocked run is Linux-only and there is no network-call trace; the knowledge format is version 1 and unknown formats are refused. `README.md` and `docs/user-guide.md` already carry the first two statements.

## Publication — `RepoLore.Cli` 0.1.0-preview.1 published 2026-10-24 (Trusted Publishing)

Decision (2026-10-24): publish with NuGet.org **Trusted Publishing** from GitHub Actions instead of a long-lived API key. The repository is public, so a policy activates immediately (the 7-day pending state in the NuGet docs applies to private repositories). NuGet issues a single-use, one-hour API key in exchange for the workflow's OIDC token; nothing secret is stored except the nuget.org username, kept as a secret rather than in the file. Source: [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing), read 2026-10-24.

`.github/workflows/publish.yml` (triggers: a `v*` tag push, or a manual run) has two jobs:

- **build** (no secrets, no OIDC permission): read the version from `Directory.Build.props`; for a tag run, require the tag to equal `v<version>`; locked restore, Release build, the Core, Infrastructure, Cli and Packaging suites on Ubuntu; `dotnet pack --no-build` of the single tool package; `build/smoke-exact-package.sh` installs **that exact `.nupkg`** from a `<clear/>` local-only feed into an isolated pinned manifest and runs `version`, `init`, `health-check`; record `sha256sum` in a `.sha256` file and the step summary; upload both as an artifact.
- **publish** (runs only from a tag, and for a manual run only if `publish` is ticked): bound to the GitHub environment `nuget-preview`; `id-token: write`; downloads the artifact, re-verifies the checksum, exchanges the OIDC token with `NuGet/login` pinned to the verified commit of v1.2.0 (`8d196754b4036150537f80ac539e15c2f1028841`; the floating `v1` tag is an annotated tag whose peeled commit is the same), and runs `dotnet nuget push` without `--skip-duplicate`, so republishing a version fails.

Checked locally on 2026-10-24: both workflow files parse as YAML; the smoke script passes on a fresh `dotnet pack --no-build` of the package and fails with exit 1 for a wrong expected version, exit 2 for a missing file or missing arguments; the version `sed` and the tag-equals-version test accept `v0.1.0-preview.1` and reject `v0.1.0` and `0.1.0-preview.1`. **Not checked:** the workflow has never run on GitHub (no `actionlint` or `shellcheck` was available), and the NuGet policy does not exist yet.

One-time setup for the maintainer (nothing here can be done from the repository):

1. nuget.org → username menu → **Trusted Publishing** → add a policy owned by you: Repository Owner `R-Elias`, Repository `repolore`, Workflow File `publish.yml` (file name only), Environment `nuget-preview`. In the scopes, allow publishing **new packages** and new versions for the package pattern `RepoLore.Cli`. If the first push of a not-yet-existing ID is refused despite that scope, the fallback is a one-time, narrowly scoped API key push of the same artifact, and the policy then governs later versions.
2. GitHub → Settings → Environments → `nuget-preview`: add yourself as required reviewer and restrict deployments to tags `v*`. Add the environment secret `NUGET_USER` = your nuget.org **profile name**, not your email.
3. Recommended: a tag ruleset that blocks deleting or moving `v*` tags.
4. Optional, later: request reservation of the `RepoLore.` prefix on nuget.org.

The steps below were followed in this order on 2026-10-24.

**Result (2026-10-24).** Tag `v0.1.0-preview.1` on `main` commit `e14096784bb70d549bd264c1dc160906b6c5ba20`. The dry run (manual, `publish` unticked) was green with package hash `5b695a9d…`. The tag run's build job produced `RepoLore.Cli.0.1.0-preview.1.nupkg` with SHA-256 `b2d46debec1e453e16355cd2accc376274c79a1a0819250d025897abed5c63dd` (a different value from the dry run, because the package is rebuilt). The first publish attempt was refused ("Tag … is not allowed to deploy to nuget-preview") because the environment's `v*` rule was a branch rule; the maintainer changed it to a tag rule, re-ran only the failed job, approved the deployment, and reported all green (the Actions run itself was not inspected from this environment). No push happened before the fix.

Post-publication checks run by the assistant from macOS arm64:

- The flat-container index listed `0.1.0-preview.1` about 4 minutes after the push; the registration and search endpoints followed a few minutes later, and the first `dotnet tool install` attempt in that gap answered "version not found". After they caught up, the same command succeeded.
- The downloaded file's SHA-256 is `a770ceaeb9e088f9010740266c9fc485218a04014c28fcc9911fafa7a4984580`, not `b2d46deb…`: NuGet.org added `.signature.p7s`. The other entries are exactly the whitelist plus the metadata part. `dotnet nuget verify` on it reports `Signature type: Repository`, subject NuGet.org Repository by Microsoft, content hash `iahCqEr1Q08X2aua77Ioqhx8FdbKxC0ClVCWp9XEoa8XDL3dM0Q16fiYV8OiJJAR1Y96dnHjOBjkXCZI+nFhbw==`. The pushed file's hash (`b2d46deb…`) is the one to cite for the artifact the workflow pushed; the downloaded file differs by the signature only.
- The nuspec on nuget.org: id `RepoLore.Cli`, version `0.1.0-preview.1`, authors `Elias Aliche`, license Apache-2.0, readme `README.md`, package type `DotnetTool`, repository commit `e140967…`, no dependencies.
- Clean install: a new consumer directory, a `<clear/>` config with only `https://api.nuget.org/v3/index.json`, empty NuGet/HTTP/plugin caches and an isolated `DOTNET_CLI_HOME`; `dotnet tool install RepoLore.Cli --version 0.1.0-preview.1 --local`, then `version` (`RepoLore 0.1.0-preview.1`, formats 1), `init` (checkpoint 1), `health-check` (healthy, exit 0), `context src/` (the authored sentinel appears) and `checkpoint` (checkpoint 2). The restored files came from the NuGet cache, not a build output.

**Package page checked 2026-10-24** (fetched page text plus the maintainer's search screenshot): prerelease notice, .NET tool, .NET 10.0, no dependencies, owner `EliasAliche`, Apache-2.0, project and repository links, README rendered with its Install/Use/License sections and the "not signed … not an enterprise-ready or certified release" statement, no deprecation or vulnerability warning. The page's first install tab is the global form (the guide recommends the pinned local manifest and labels global as untested); no package icon is set, which the page does not flag. **Not done:** creating the GitHub pre-release for the tag (needs the label above and the `b2d46deb…` hash); the optional `RepoLore.` prefix reservation. **Follow-ups, none blocking:** the workflow warns that the v4 actions run on a forced Node 24 and that `ubuntu-latest` is migrating to Ubuntu 26 (pin the runner image and bump the actions in a later commit, which needs a new version to take effect); a committed tool manifest pinning `0.1.0-preview.1` can now be added to this checkout if the maintainer wants it; the published readme says "not signed" where "no author signature" would be more precise, which can be corrected in the next version.

Original ordered steps:

1. Commit and push `publish.yml` and the script on `feature/v1-release`; wait for CI green, then merge the branch into `main` by pull request. GitHub offers the manual **Run workflow** button only for a workflow that exists on the default branch; a tag run uses the workflow file at the tagged commit.
2. Dry run: Actions → publish → Run workflow on `main` with `publish` unticked. Expect the build job green, a `package` artifact, and the SHA-256 in the run summary; the publish job is skipped.
3. Tag the same `main` commit `v0.1.0-preview.1` and push the tag. The build job reruns; the publish job waits for your approval, then pushes. A published version can be unlisted but not deleted, so read the checksum in the summary before approving.
4. After NuGet.org indexes the package: in a clean directory with only nuget.org as a source, `dotnet tool install RepoLore.Cli --version 0.1.0-preview.1 --local` and `repolore version`; look at the readme rendering and the displayed author.
5. Create a GitHub pre-release for the tag carrying the preview label (preview; unsigned; not enterprise-ready or certified; .NET 10 SDK to install, 10.0.x runtime to run; networking-blocked run is Linux-only and there is no network-call trace; format version 1) and the SHA-256 of the pushed file. NuGet.org adds its repository signature after upload, so a later download can differ in bytes from the artifact; the recorded hash is of the file that was pushed.

## Maintenance notes

- Adding a file to the package means editing the whitelist in `PackageContentsTests` on purpose; adding a documented step to the journey means editing both the test and `docs/user-guide.md` so the doc stays the single order a person follows.
- `IsolatedToolEnvironment.Dispose` clears read-only attributes first: Git object files are read-only and block recursive delete on Windows.
- Windows safety: the journey passes `core.autocrlf=false` to every Git call and writes LF text, so the pinned hashes hold in a fresh clone; this is unverified on Windows runners until CI runs.
