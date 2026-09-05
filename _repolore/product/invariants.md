# RepoLore Invariants

These constrain the planned application and every future implementation decision. Details: [roadmap](../roadmap/roadmap.md).

1. **Absolute privacy by design within RepoLore.** No telemetry, accounts, remote AI, uploads, update checks, DNS, HTTP, or other outgoing network operations in the CLI/core. No silent collection or transmission of user data.
2. **100% local operation.** After installation, built-in features need only local files. Package-manager traffic and external agents/editors are separate systems; RepoLore does not claim to control their behavior.
3. **Too stupid to fail.** Prefer short, deterministic, inspectable operations. No daemon, watcher, database, or speculative framework. Fail clearly rather than guess. A small exclusive-write guard is allowed to protect recovery state.
4. **One durable knowledge model.** `sparse-tree/` and user-defined long-term areas are editable knowledge, never disposable generated output. No complete empty mirror. Missing notes are normal.
5. **Preserve before destructive change.** Tool-managed overwrite, deletion, migration, and restore require recoverable prior contents. Failed preservation blocks the operation. Never silently discard conflicting knowledge.
6. **Recovery has honest limits.** Explicit checkpoints capture observed states, including eligible Gitignored session notes. History is configurable and bounded; uncaptured edits and evicted versions are not promised recoverable.
7. **Policies stay separate.** Discovery exclusions, Git tracking, and history coverage are independent. Ignoring a source path never authorizes deleting its knowledge.
8. **Reads and previews do not write.** No hidden checkpoint, migration, normalization, or cleanup during context reads, inspection, or dry-runs.
9. **Compatibility is explicit.** Unsupported formats fail safely. Format changes require deliberate migration and recovery; package updates do not silently migrate knowledge.
10. **Sessions stay explicit and local.** Short-term knowledge lives in Gitignored session folders. Never auto-load unrelated sessions, treat provisional notes as established guidance, auto-promote findings, or expire current session files. Sessions and recovery history are distinct.
11. **Small maintenance burden.** Deterministic tests protect the core contract. v1 publishes one NuGet CLI package and contains no plugins. Future official plugins must be local-only; third-party plugins have a separate trust boundary.
