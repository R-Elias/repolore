# Root — repolore-poc

This repository is the proof of concept of RepoLore: operational memory for developers and coding agents, stored as local Markdown. It contains the method, PowerShell alpha tools, and the .NET CLI foundation: `version`/`--help`, the read-only knowledge commands `path`, `context`, and `tree` (durable path notes plus explicitly selected session notes), the local `checkpoint`/`history` snapshot capture, `restore` with interrupted-operation recovery, minimal `init`/`init --update-method`, and explicit `migrate` (alpha-to-v1 with `--check`/`--dry-run`). NuGet installation remains planned.

## Where to start

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
- Understand the current alpha method → repository-root `method.md` and its matching `_repolore/method.md` copy.
- Work on the PowerShell alpha tools → `tools/tools.md` and `_repolore/sparse-tree/tools/tools.md`.
- Work as an agent → `AGENTS.md` for contract reading order, package execution/gates, alpha/v1 boundaries, and durable-update/session responsibilities.

## Current state versus target contract

The alpha full mirror (`_repolore/tree/`) has been removed; `_repolore/sparse-tree/` is the authored canonical path tree (currently `tools/tools.md`). The PowerShell scripts still describe the alpha mirror behavior as a historical reference. Do not regenerate the full mirror; a sparse-only clone holds authored knowledge only in `sparse-tree/`.

The full mirror is gone and `sparse-tree/` is the sole authored path tree. Custom areas are first-class. Planned `sessions/<session-id>/` folders add Gitignored short-term knowledge, explicitly selected for context and included in checkpoint recovery by default. Durable knowledge remains the reviewed long-term layer. `product/` and `roadmap/` are authored planning areas today and may be edited directly. Do not run alpha generators on a migrated v1 knowledge base.

The first release is one NuGet .NET tool package, `RepoLore.Cli`, containing Core and Infrastructure assemblies. No plugins or additional package channels. Runtime target: .NET 10. The core is local-only; recovery uses explicit checkpoints with a configurable 200 MiB default budget.

The old alpha notes are historical implementation references. The v1 roadmap supersedes their full-mirror and exact-command-compatibility assumptions. Both method copies remain synchronized and explicitly labeled alpha until the new implementation and migration land.

## Remaining design work

The [implementation guide](roadmap/implementation-v1.md) resolves path escaping, context ordering/budgets, ignore semantics, history scope/retention, and interrupted recovery. Turn its contracts into the specified fixtures before implementing dependent mutations. Track completion there; packages 01–09 (executable foundation, safe paths and mapping, configuration and coverage policies, durable and session context, snapshot capture and publication, retention and mutation ownership, restore and interrupted-operation recovery, initialization and method updates, alpha migration and rollback) are implemented with local checks, with CI/platform gates tracked separately. Verified NuGet installation does not yet exist. Later plugin/distribution releases have no committed version or date.
