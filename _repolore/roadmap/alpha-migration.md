# Alpha migration and rollback — package 09

`migrate [--check | --dry-run]` inventories both alpha trees, classifies every note mapping, and
(on a clean apply) migrates alpha knowledge to v1 without choosing the user's knowledge. It builds
on packages 02 (path mapping), 05–07 (capture, retention, pending recovery), and 08 (alpha/v1
classification); see [paths-and-mapping](paths-and-mapping.md), [snapshot-capture](snapshot-capture.md),
[restore-and-recovery](restore-and-recovery.md), and [initialization-and-method-updates](initialization-and-method-updates.md).

## Folder layout

```text
src/RepoLore.Core/Migration/MigrationPlan.cs   NoteSource + NoteVariant + MappingStatus + MigrationMapping + MigrationPlanner
src/RepoLore.Infrastructure/History/
  MigrationEnumerator.cs                       migration-scope file walk (tree/ + sparse-tree/ + top-level root.md + repolore.json)
  MigrationEngine.cs                           MigrationException + MigrationInventory + MigrationResult + MigrationEngine
src/RepoLore.Cli/Commands/MigrateCommand.cs
```

Core stays pure (classification only); Infrastructure owns inventory, baseline capture, the
pending transaction, and mutation; the CLI orchestrates and renders.

## Classification (pure)

`MigrationPlanner.Classify(notes)` groups variants by decoded directory note. For each note:

- **candidate** — one useful variant (copy the `tree/` variant or keep the `sparse-tree/` variant),
  or several identical useful variants (sparse wins; no copy);
- **conflict** — differing useful variants (`tree/` vs `sparse-tree/`, or either vs a differing
  top-level `root.md`);
- **ambiguous** — an old note name that does not decode to its directory (`foo/bar.md`), reported as
  a conflict even though a source directory exists.

Empty nodes (`<!-- repolore:empty -->`) never win over a useful variant; only `_repolore/root.md`
escapes `tree/` to the top-level destination. Destination files are never overwritten because they
are "derived": a note already at its destination is a keep (no copy), and the top-level `root.md`
wins over a `tree/root.md` of identical text.

## Inventory (physical)

`MigrationEngine.Inventory()` walks `_repolore/tree/` and `_repolore/sparse-tree/` recursively. It
records Markdown notes as variants; non-Markdown and non-UTF-8 files are `UnknownFiles` (left in
place and reported); every `tree/` Markdown is staged for removal; a stray `sparse-tree/root.md`
is staged for removal. Custom top-level areas, session folders (valid session ids), and a
`_repolore/sessions/` whose entries do not match the session layout are reported separately; that
naming conflict blocks apply.

## Migration scope

A migration baseline is captured with `CheckpointEngine.Capture(config, migrationScope: true)`,
which enumerates the extended physical scope (`MigrationEnumerator`) instead of ordinary v1
coverage: the legacy `tree/`, the destination `sparse-tree/`, the top-level `root.md`, and the
still-absent marker. The manifest records `scope.migrationScope: true` (omitted when false, so old
manifests decode to false and remain compatible). `RestoreEngine` reads
`target.Scope.MigrationScope` to choose the enumerator and skip `history.exclude` filtering, so
restoring a migration baseline restores old bytes, removes only migration-created destinations,
and restores marker absence. Rerunning a clean migration is a no-op (already `V1`).

## Apply transaction

`Apply` reuses package-07's pending record unchanged: capture the migration baseline, write
`pending.json` (baseline id as both target and pre-operation id, plus `plan[{path, before, after}]`
covering copies, verified legacy removals, and the marker), then stage each write via temp-file
rename and recheck the before-state, remove only verified redundant legacy files, prune now-empty
`tree/` directories, and write the marker last. An interruption reports the baseline id; the
existing `restore <baseline-id>` route recovers because `PendingCodec`/`RestoreEngine.Recover` are
scope-agnostic.

## Maintenance notes

- The marker `{"formatVersion":1}` is a literal string; template hashes/writes round-trip through
  files (no `Encoding.UTF8.GetBytes` — it trips the build guard like `GetByteCount` does).
- No `record` types (the guard rejects the generated `System.Type`); all model types are sealed
  classes.
- Read-only commands never migrate: `context`/`path` still read both alpha copies (package 04);
  `init` still refuses alpha; migration is explicit and deliberate only.
- Conflict resolution is a human step: a separate explicit `checkpoint` (ordinary v1 scope) before
  editing protects the variants while a human resolves them.
