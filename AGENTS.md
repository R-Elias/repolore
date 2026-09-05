# Working on RepoLore

RepoLore is an open-source, local knowledge tool maintained by one developer. Keep changes small, deterministic, and reviewable. The repository uses RepoLore as its own operational knowledge; read it before broad source exploration.

## Start with the contract

Before exploring, modifying, reviewing, or auditing an area:

1. Read [`_repolore/method.md`](_repolore/method.md) and [`_repolore/root.md`](_repolore/root.md).
2. Read the relevant path notes and related knowledge for the area; inspect source only as needed for the task.
3. For v1 work, read [product direction](_repolore/product/product.md), [invariants](_repolore/product/invariants.md), and the [roadmap](_repolore/roadmap/roadmap.md).
4. Before implementation, read the [first-release work packages](_repolore/roadmap/implementation-v1.md), including the requested package and its dependencies. Read the [session contract](_repolore/product/sessions.md) when handling short-term knowledge or session behavior.
5. Inspect the working-tree status and existing implementation/check evidence. Preserve unrelated and user-authored changes; do not assume an unchecked item means no code exists.

The product documents define intended behavior; the implementation guide resolves detailed first-release choices. Its pass/fail gates are required. Do not replace specified path escaping, context ordering, history scope, or recovery rules with an easier approximation. If these documents conflict, identify the exact conflict rather than silently choosing a convenient interpretation.

Code remains the source of truth for current behavior. Verify discrepancies and update stale implementation notes. A bug does not redefine an intended requirement: surface that conflict and fix within the authorized task.

## Distinguish alpha from v1

The current method copies and PowerShell scripts describe the alpha full mirror and generated sparse tree. They are historical implementation references, not the v1 specification. The .NET CLI, session-aware commands, and recovery features are planned; verify what has actually landed before using or claiming them.

- Do not recreate the complete empty mirror in v1. Its canonical authored path notes live in `_repolore/sparse-tree/`, alongside custom durable areas.
- Do not delete, migrate, or regenerate this checkout's knowledge just because the design changed. A sparse-only clone may contain its only knowledge copy there.
- Never run alpha tree generators on a migrated v1 knowledge base. Test migration/restore on disposable fixtures first; follow the guide's backup and preservation gates before dogfood migration.
- `_repolore/product/` and `_repolore/roadmap/` are directly authored planning areas today. For existing alpha path notes, follow the alpha method, but do not regenerate sparse-tree unless the complete source has been verified to preserve all authored knowledge.
- Keep repository-root `method.md` and `_repolore/method.md` identical when changing the method. Replace their alpha guidance only when the corresponding v1 behavior and migration are implemented.

## Execute implementation in bounded steps

Work on the user's requested scope. For a general request to continue v1, select the earliest incomplete work package whose prerequisites are satisfied by implementation and passing checks. Name that package and its gate before editing. Do not skip a failed prerequisite to reach a later feature.

For each work package:

1. Identify its deliverable, prohibited shortcuts, and pass/fail fixtures in the guide.
2. Implement the smallest complete behavior that meets that contract. Use the existing shared path, policy, and recovery components; avoid alternate implementations in individual commands.
3. Run its specified checks and the relevant existing regression checks. Verify negative/failure behavior, not just the happy path. Fix failures within scope instead of weakening assertions or asking for routine design approval.
4. Update affected durable knowledge and the implementation completion record with the behavior delivered, exact check commands/results, fixture names, and remaining limitations.
5. Mark the package complete only when its required gates passed. Distinguish local validation from CI/platform checks not yet run. Compilation, a stub returning success, or a documentation edit alone is not completion.

Use one deterministic test project with small fixtures, injected time/IDs, and narrow failure injection. Do not introduce a benchmark service, broad research program, speculative framework, or duplicate test infrastructure. After required checks pass, broaden testing only for a specific unresolved risk or regression.

## Boundaries that must survive every change

- **Local and private:** the shipped CLI/core performs no network operations, telemetry, remote AI calls, update checks, or subprocess execution. Build/package tooling and external agents are outside that application boundary; do not claim control over them.
- **One release package:** v1 ships `RepoLore.Cli` as a NuGet .NET tool with Core/Infrastructure assemblies included. No separate Core package, plugins, additional package ecosystems, or hosted service.
- **Simple execution:** no daemon, watcher, database, or semantic index. The specified small exclusive writer guard protects mutations; it is not a background service.
- **Preserve before mutation:** do not enable destructive handlers until capture, retention, and recovery gates pass. A failed pre-operation checkpoint blocks overwrite/delete/migration. Retention must preserve the required undo state.
- **Read-only means read-only:** reads, health checks, and previews must not checkpoint, migrate, normalize files, or clean up history.
- **Independent policies:** source discovery, Git tracking, and history coverage are separate. An ignore rule never authorizes deleting knowledge.
- **Explicit compatibility:** unknown formats fail safely. Migration is deliberate and recoverable; no silent alpha interpretation or conflict resolution.

The linked invariants and implementation guide contain the complete contract. Do not relax them to make a release or test pass.

## Sessions and durable updates

Sessions are optional local working knowledge under `_repolore/sessions/<session-id>/`, not a replacement for durable RepoLore updates. Select a session explicitly; do not automatically load unrelated or newest sessions. Notes remain provisional until a useful finding is verified and incorporated into the appropriate durable node through normal review.

Before creating sessions, ensure session/history paths are Gitignored; never commit their contents. Gitignore does not untrack previously tracked files. Session folders and recovery history are different things. Do not delete sessions or expire their contents automatically.

When checkpoint tooling is implemented and validated, checkpoint before and after direct knowledge edits, including session edits. Until then, do not invent a working checkpoint command or claim recovery protection. Follow the documented backup procedure for any authorized destructive operation.

After meaningful changes, update RepoLore for behavior, architecture, responsibilities, dependencies, conventions, important paths, or maintenance assumptions. Keep notes concise and evidence-backed. Do not duplicate obvious source code or record guesses as facts. A session-only note is insufficient for a change future maintainers need to understand.

## Handoff and completion

Finish with what changed, which work package/gates were addressed, checks actually run and their results, and any remaining blocker or next dependency. Keep the completion record consistent with that report. Do not claim implementation, migration, publication, or cross-platform validation that did not happen. Planning instructions are not authorization to publish a release or discard existing work.
