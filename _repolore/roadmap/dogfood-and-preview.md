# Dogfood and NuGet preview — package 12

Prepares the preview; **nothing is published and this checkout is not migrated**. Gates: [implementation guide](implementation-v1.md#12--exercise-the-complete-workflow-and-publish-a-preview). Depends on [packaging and installation](packaging-and-installation.md) (package 11). Behavior referenced here is in [durable and session context](durable-and-session-context.md), [snapshot capture](snapshot-capture.md), [restore and recovery](restore-and-recovery.md), [initialization](initialization-and-method-updates.md), [alpha migration](alpha-migration.md), and [health and CLI contract](health-and-cli-contract.md).

## What package 12 delivers

| Deliverable | Where |
|---|---|
| Install, internal feed, daily use, sessions, format/migration, recovery instructions | `docs/user-guide.md` (user-facing, outside `_repolore/`) |
| Package readme that states the preview/unsigned label | `README.md`, packed as `PackageReadmeFile` (whitelist test updated deliberately) |
| One scripted acceptance journey on disposable fixtures | `tests/RepoLore.Cli.Tests/Packaging/Journey/AcceptanceJourneyTests.cs` |
| Method banner and agent guidance reflecting implemented behavior | both `method.md` copies (banner only, identical), `AGENTS.md` |
| Dogfood backup procedure, preview checklist, publication steps | this note (prepared, not executed) |

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

## Dogfood migration of this checkout — prepared, NOT run

Rehearsed on 2026-10-23 on a disposable `git clone` of HEAD (never on this checkout): `migrate --check` exit 1; `--dry-run` plans one `create _repolore/repolore.json` and keeps `root.md` and `sparse-tree/tools/tools.md`; `product/` and `roadmap/` are reported as untouched custom areas. After `migrate`: SHA-256 of all 19 files unchanged (`diff` of before/after manifests empty), `repolore.json` is `{"formatVersion":1}`, `health-check` healthy, second `migrate` is a no-op, `restore 1` (the migration baseline) deleted only the marker and `health-check` reported `migration-needed` again.

Procedure to run only after the maintainer says so:

1. `git status` — preserve every uncommitted change; commit or stash deliberately, never discard.
2. Outside the repository **and outside `_repolore/.history/`**: `git bundle create <backup-dir>/repolore-poc-<date>.bundle --all`, `cp -R _repolore <backup-dir>/_repolore`, `cp method.md .gitignore <backup-dir>/`.
3. Verify: `git bundle verify <bundle>`; run `find _repolore -type f -print0 | sort -z | xargs -0 shasum -a 256` on the live and backup copies and require an empty `diff`.
4. Install the tool from the local feed in a manifest **outside** this checkout (this checkout deliberately has no manifest yet) and rehearse once more on a fresh clone.
5. In the checkout: `repolore migrate --check`, `--dry-run`; confirm the plan equals the rehearsal.
6. `repolore migrate`; then `health-check`; compare the file manifest (only `repolore.json` added).
7. Decide separately about `init --update-method` (replaces the alpha `_repolore/method.md` with shipped v1 guidance) and about the repository-root `method.md`, which AGENTS.md requires to stay identical to `_repolore/method.md`. Remove the obsolete `_repolore/tree/` line from `.gitignore` only deliberately.
8. To undo: `restore <baseline id>`; if that were ever insufficient, restore from the verified backup.

## Preview readiness

| Item | State |
|---|---|
| Package builds, installs from a clean local-only feed, runs the full journey | **Ready** locally on macOS arm64; Windows/Linux/macOS CI for the new tests **not run** (needs a push) |
| Contents whitelist, no knowledge/sessions/history/test stack/build paths | **Ready** (readme added deliberately) |
| User documentation and package readme | **Ready**; NuGet.org rendering of the readme unseen until a publish |
| `RepoLore.Cli` ID ownership, `RepoLore.` prefix reservation | **Blocked** — needs a nuget.org account; a read-only lookup showed the ID unpublished (2026-10-23), which proves nothing about ownership |
| `Authors` (`R-Elias`), `PackageProjectUrl`/`RepositoryUrl` (`https://github.com/R-Elias/repolore`) | **Blocked on a human decision**; URL equals `origin` but the repository's visibility and the author identity were not verified |
| Package signing | **Not available**; a preview ships unsigned; v1.0.0 gate (package 13) |
| Network-call trace of the smoke path | **Not implemented**; static guards, the boundary-guard build, and the Linux networking-blocked run are the evidence |
| Linux networking-blocked run of the journey | **Written, never run**; the journey (and its Git calls) now runs inside that step in CI |
| Dogfood migration of this checkout | **Blocked on maintainer approval** |
| SBOM, checksum file, vulnerability-reporting contact, release notes | Package 13 |

**The preview must state:** it is a preview (`0.1.0-preview.1`); the package is unsigned (NuGet.org adds only its own repository signature after upload, which is not an author signature); it is not enterprise-ready, certified, or a guarantee about what external agents or editors do with content they read; it needs a .NET 10 SDK to install and a .NET 10.0.x runtime; the networking-blocked run is Linux-only and there is no network-call trace; the knowledge format is version 1 and unknown formats are refused. `README.md` and `docs/user-guide.md` already carry the first two statements.

## Publication steps — prepared, not executed

Only on an explicit instruction in a session, after the blocked rows are settled and CI is green on the exact commit:

1. Tag nothing yet. On a clean checkout of the candidate commit: `dotnet restore --locked-mode`; `dotnet build -c Release --no-restore`; the three suites and the Packaging suite.
2. `dotnet pack src/RepoLore.Cli -c Release --no-restore -o <artifact-dir>`; record `shasum -a 256` of the single `.nupkg`.
3. Smoke the exact file: a feed directory holding only that `.nupkg`, a `<clear/>` config, empty caches, `dotnet tool install --local`, `repolore version`. (The Packaging tests pack their own copy; they do not test the uploaded bytes.)
4. `dotnet nuget push <nupkg> --source https://api.nuget.org/v3/index.json --api-key $NUGET_API_KEY` with the key from the environment, never from source. A published version can be unlisted but not deleted.
5. After NuGet.org indexes it: install `RepoLore.Cli --version 0.1.0-preview.1` into a clean directory and check `repolore version`; inspect the readme rendering.
6. Tag `v0.1.0-preview.1` on the published commit and create a GitHub pre-release carrying the label above and the hash from step 2 (repository signing can change bytes after upload; record the hash of the artifact you pushed).

## Maintenance notes

- Adding a file to the package means editing the whitelist in `PackageContentsTests` on purpose; adding a documented step to the journey means editing both the test and `docs/user-guide.md` so the doc stays the single order a person follows.
- `IsolatedToolEnvironment.Dispose` clears read-only attributes first: Git object files are read-only and block recursive delete on Windows.
- Windows safety: the journey passes `core.autocrlf=false` to every Git call and writes LF text, so the pinned hashes hold in a fresh clone; this is unverified on Windows runners until CI runs.
