# Root — repolore-poc

This repository is the proof of concept of RepoLore: operational memory for developers and coding agents, stored as local Markdown. It contains the method and PowerShell alpha tools; the .NET CLI is planned, not implemented here.

## Where to start

- Understand the user's philosophy, desired experience, and release objectives → [product direction](product/product.md).
- Understand short-term knowledge, session selection, and promotion → [session contract](product/sessions.md).
- Check absolute application constraints → [invariants](product/invariants.md).
- Plan or build the first stable CLI release → [v1 roadmap and implementation contract](roadmap/roadmap.md).
- Implement the first release step by step → [implementation work packages and pass/fail gates](roadmap/implementation-v1.md).
- Understand the current alpha method → repository-root `method.md` and its matching `_repolore/method.md` copy.
- Work on the PowerShell alpha tools → `tools/tools.md` and `_repolore/sparse-tree/tools/tools.md`.
- Work as an agent → `AGENTS.md` for contract reading order, package execution/gates, alpha/v1 boundaries, and durable-update/session responsibilities.

## Current state versus target contract

The current alpha uses a gitignored `_repolore/tree/` and a committed generated `_repolore/sparse-tree/`. The scripts still implement that behavior. Do not delete or regenerate either as part of a planning edit; a fresh clone may hold its only knowledge copy in sparse-tree.

The planned v1 removes the full mirror and makes `sparse-tree/` the sole authored path tree. Custom areas are first-class. Planned `sessions/<session-id>/` folders add Gitignored short-term knowledge, explicitly selected for context and included in checkpoint recovery by default. Durable knowledge remains the reviewed long-term layer. `product/` and `roadmap/` are authored planning areas today and may be edited directly. Do not run alpha generators on a migrated v1 knowledge base.

The first release is one NuGet .NET tool package, `RepoLore.Cli`, containing Core and Infrastructure assemblies. No plugins or additional package channels. Runtime target: .NET 10. The core is local-only; recovery uses explicit checkpoints with a configurable 200 MiB default budget.

The old alpha notes are historical implementation references. The v1 roadmap supersedes their full-mirror and exact-command-compatibility assumptions. Both method copies remain synchronized and explicitly labeled alpha until the new implementation and migration land.

## Remaining design work

The [implementation guide](roadmap/implementation-v1.md) resolves path escaping, context ordering/budgets, ignore semantics, history scope/retention, and interrupted recovery. Turn its contracts into the specified fixtures before implementing dependent mutations. Track completion there; all work packages remain unimplemented. Session-aware commands, checkpoint commands, migration, and NuGet packaging do not yet exist. Later plugin/distribution releases have no committed version or date.
