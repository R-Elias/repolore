# Root — repolore-poc

This repository is RepoLore itself: the `repolore` command (a .NET tool, and native executables for the GitHub release, PyPI, npm, Homebrew, winget and repolore.org), its method, and the planning notes that produced it. It is also its own first user: this checkout is a format 1 knowledge base with local Gitignored history (`.history/`), a committed tool manifest (`.config/dotnet-tools.json`) that pins a published release, and path notes for its source areas under `sparse-tree/`. What each release shipped and how it was verified is recorded in [distribution](roadmap/distribution.md) and the [v1.0.0 release plan](roadmap/release-v1.md); the user manual is [the user guide](../docs/user-guide.md).

## Where to start

- Change source code, tests, build scripts, workflows, docs or the website → run `repolore context <path>` for the path first (for example `src/RepoLore.Infrastructure/History/`). The notes in `sparse-tree/` cover `src/` and its three projects, `History/`, `tests/`, the scenarios, `build/`, `.github/workflows/`, `docs/` and `site/`.
- Change a root file (the version, lock files, the SDK pin, the README as package readme, the tool manifest, the method copies) → [Root files](#root-files).
- Understand the user's philosophy, desired experience, and release objectives → [product direction](product/product.md).
- Understand short-term knowledge, session selection, and promotion → [session contract](product/sessions.md).
- Check absolute application constraints → [invariants](product/invariants.md).
- Plan or build the first stable CLI release → [v1 roadmap and implementation contract](roadmap/roadmap.md).
- Implement the first release step by step → [implementation work packages and pass/fail gates](roadmap/implementation-v1.md).
- Work on the .NET executable, build guards, or deterministic tests → [executable foundation](roadmap/executable-foundation.md).
- Work on durable/session context, path/context/tree commands, budget estimation, or session IDs → [durable and session context](roadmap/durable-and-session-context.md).
- Work on checkpoint/history capture, the writer guard, manifest format, or object storage → [snapshot capture](roadmap/snapshot-capture.md).
- Work on restore, pending transactions, or interrupted-operation recovery → [restore and recovery](roadmap/restore-and-recovery.md).
- Work on initializing a checkout or updating the shipped method → [initialization and method updates](roadmap/initialization-and-method-updates.md).
- Work on migrating alpha knowledge to v1 → [alpha migration and rollback](roadmap/alpha-migration.md).
- Work on `health-check`, finding IDs, exit codes, JSON error results, or option validation → [health checks and CLI contract](roadmap/health-and-cli-contract.md).
- Work on the NuGet tool package, package contents, install/restore from a local feed, or the network-blocked smoke → [packaging and installation](roadmap/packaging-and-installation.md).
- Use the tool (install, internal feed, daily use, sessions, migration, recovery) → [user guide](../docs/user-guide.md).
- Plan or work on the scenario tests (reference repository, scenarios, stages, schedule) → [scenario tests](roadmap/scenario-tests.md).
- Work on the native executables, the channels other than NuGet, the publish workflow's checkboxes or the website → [distribution](roadmap/distribution.md).
- Plan or work on the v1.0.0 release (author signing deferred; SBOM, release notes, vulnerability reporting, remaining actions) → [v1.0.0 release plan](roadmap/release-v1.md).
- Prepare or review the dogfood migration of this checkout, the acceptance journey, or the preview-readiness checklist and publication steps → [dogfood and preview](roadmap/dogfood-and-preview.md).
- Read the method (the shipped v1 guidance; the two copies are identical) → repository-root `method.md` and its matching `_repolore/method.md` copy. The alpha text it replaced is in Git history.
- Work as an agent → `AGENTS.md` for contract reading order, package execution/gates, alpha/v1 boundaries, and durable-update/session responsibilities.

## Root files

Root files that every change touches or must not break. The agent contract is `AGENTS.md`; the quality policy is `ENGINEERING.md`.

- **The version has one source:** `<Version>` in `Directory.Build.props`. The CLI reads it at build (a generated `CliVersion`), and the NuGet package, the native executables and every wrapper take it from there.
- **Lock files carry the version.** Project references appear in them as `"RepoLore.Core": "[1.1.1, )"`, so a version change fails the locked restore CI uses until both kinds are regenerated: `dotnet restore --force-evaluate`, then `dotnet restore src/RepoLore.Cli/RepoLore.Cli.csproj -p:RepoLoreNative=true --force-evaluate`. Only the version should change in the diff. Native builds keep their own lock files, `src/*/packages.native.lock.json` ([distribution](roadmap/distribution.md)).
- **The SDK is pinned:** `global.json` fixes `10.0.100` with `rollForward: disable`, so another SDK refuses to build. `Directory.Build.props` also turns warnings into errors and makes builds deterministic (`DeterministicSourcePaths` rewrites build paths to `/_/...`).
- **`README.md` is also the NuGet package readme.** The package tests require it to equal the packaged readme and to contain "no author signature" and "not an enterprise-ready or certified release".
- **`method.md` and `_repolore/method.md` stay identical.** The shipped text is `EmbeddedTemplates.Method` in the CLI; this checkout's copies change only through `repolore init --update-method`, then copying the result to the root copy.
- **`.config/dotnet-tools.json` pins the published tool this checkout runs.** A bump is a reviewed change: run `dotnet tool restore`, then `version`, `history`, `checkpoint` and `health-check` with the new pin.
- **`Directory.Build.targets`** holds the runtime-boundary guard ([src](sparse-tree/src/src.md)).
- **`.gitignore`** keeps `_repolore/sessions/` and `_repolore/.history/` out of Git. There is no `.repoloreignore`, so the default source-discovery exclusions apply (`bin/`, `obj/`, `node_modules/`, `build/`, `vendor/`).
- **The root note is this file.** The repository root's note is `_repolore/root.md`; do not add `sparse-tree/root.md`, which `health-check` reports as `conflicting-alpha-notes` against it.

## Current state versus target contract

`_repolore/sparse-tree/` is the authored canonical path tree, with notes for the source areas listed under "Where to start". The alpha full mirror (`_repolore/tree/`) has been removed, and the alpha PowerShell tools were removed on 2026-10-24: they survive in Git history only, as a historical reference, and must never be run. Do not regenerate the full mirror; a sparse-only clone holds authored knowledge only in `sparse-tree/`.

The full mirror is gone and `sparse-tree/` is the sole authored path tree. Custom areas are first-class. Planned `sessions/<session-id>/` folders add Gitignored short-term knowledge, explicitly selected for context and included in checkpoint recovery by default. Durable knowledge remains the reviewed long-term layer. `product/` and `roadmap/` are authored planning areas today and may be edited directly. Do not run alpha generators on a migrated v1 knowledge base.

The first release is one NuGet .NET tool package, `RepoLore.Cli`, containing Core and Infrastructure assemblies; from 1.1.1 the package is named `RepoLore`. No plugins. Since 2026-10-25 the same code also ships as native executables for the GitHub release, PyPI, npm, Homebrew, winget and repolore.org ([distribution](roadmap/distribution.md)). Runtime target: .NET 10. The core is local-only; recovery uses explicit checkpoints with a configurable 200 MiB default budget.

The old alpha notes are historical implementation references. The v1 roadmap supersedes their full-mirror and exact-command-compatibility assumptions. Both method copies were replaced on 2026-10-24 with the shipped v1 method through `repolore init --update-method`, updated again on 2026-10-25 to the method that `1.0.0-rc.2` through `1.1.0` ship, and remain identical; the committed tool manifest `.config/dotnet-tools.json` pins `RepoLore.Cli` `1.1.0`; before that it pinned `0.1.0-preview.1`, the three release candidates and `1.0.0`, the first stable release ([dogfood and preview](roadmap/dogfood-and-preview.md)).

## Remaining design work

The [implementation guide](roadmap/implementation-v1.md) resolves path escaping, context ordering and budgets, ignore semantics, history scope and retention, and interrupted recovery. Its [completion record](roadmap/implementation-v1.md#completion-record) is the one place that tracks what is done and which gates remain; this note does not repeat it. What is still open is listed in [distribution](roadmap/distribution.md) (channels, the winget review, signing, Cloudflare Pages) and in the [v1.0.0 release plan](roadmap/release-v1.md) (package 14c and the post-1.0 CLI list). Plugins have no committed version or date.
