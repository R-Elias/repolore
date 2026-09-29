# Restore and interrupted-operation recovery — package 07

`restore <id> [--path <path>] [--dry-run]`: a pure plan first, then a guarded application, plus an
explicit interrupted-operation recovery route (`restore <pre-operation-id>`). Builds on packages
05 (snapshot capture) and 06 (retention pinning); see [snapshot-capture](snapshot-capture.md) and
[retention-and-mutation-ownership](retention-and-mutation-ownership.md).

## Folder layout

```text
src/RepoLore.Core/Restore/                  (RepoLore.Core.Restore)
  RestorePlan.cs                             RestoreAction + RestorePlanEntry + RestorePlan + RestorePlanner
  PendingTransaction.cs                      PendingPlanEntry + PendingTransaction + PendingCodec
src/RepoLore.Infrastructure/History/
  RestoreEngine.cs                           RestoreResult + RestoreException + PendingStore + RestoreEngine
src/RepoLore.Cli/Commands/RestoreCommand.cs
```

Core stays pure (plan computation, pending model/codec); Infrastructure owns all physical IO and
reuses `ObjectStore`/`CheckpointStore`/`CheckpointEngine`/`HistoryCleanup`/`WriterLock`/
`HistoryCoveragePolicy`/`ConfigParser`/`RepositoryPathResolver`. `--path` and `--dry-run` are parsed
by the existing `CommandLine`.

## Planning (pure)

- `RestorePlanner.Plan(target, currentFiles, currentConfig, path?)` builds the candidate union of
  saved paths and currently-eligible paths, then keeps only paths eligible under **both** the target
  manifest's `scope.exclude` and the current `history.exclude`. Classify: identical → `Unchanged`,
  saved-only → `Add`, both-but-different → `Replace`, current-only (in saved coverage) → `Delete`.
  A path excluded on either side is skipped (never deleted); an explicit `--path` excluded on either
  side throws `RestorePlanException` with a scope explanation.
- `RestorePlan.IsNoOp` is true when every in-scope entry is `Unchanged`. A no-op restore creates no
  pre-operation checkpoint and no pending record.

## Transaction and pending record

- `Apply` validates the target (unknown id, corrupt/missing after-objects) before any authored
  mutation, short-circuits on no-op, then captures the pre-operation checkpoint with the target
  pinned, preflights `HistoryCleanup.ProtectedBytes({pre-op, resulting, target}) ≤ maxBytes`, and
  writes `.history/pending.json` before the first replacement.
- `pending.json` (via `PendingCodec`) records `historyVersion`, `targetId`, `preOperationId`, and a
  frozen config `{formatVersion, historyEnabled, maxBytes, exclude}`, plus the affected
  `plan[{path, before, after}]` (hash or null). It is written atomically (temp-file rename) and is
  the only thing recovery trusts — never the possibly-damaged current `repolore.json`.
- Each write is staged to `.history/tmp/` and renamed over the destination (breaks hardlinks safely,
  never truncates in place); each file is rechecked against its recorded before-state immediately
  before replacement. The post-checkpoint is published (with the pre-op and target pinned), then
  pending is cleared. No whole-directory atomicity claim.

## Recovery

- While `pending.json` exists, `checkpoint` and a non-matching `restore` refuse with the recovery id;
  `history` and reads stay available (they never touch pending).
- `Recover(preOpId)` reads and validates pending (not current config), reconstructs the frozen
  `RepoLoreConfig`, then for each affected path validates the current content is the recorded before
  or after state (a fresh edit is refused, not overwritten), restores the before-image (or absence),
  confirms/publishes the recovered before-state checkpoint under the frozen policy, and only then
  clears pending. Retryable; no nested recovery.

## Retention pinning

- `HistoryCleanup.Clean(maxBytes, protectedIds)` never evicts the latest id or any id in the
  protected set; `ProtectedBytes(manifests)` sums manifest bytes plus deduplicated object bytes.
- `CheckpointEngine.Capture(config, beforeVerification?, beforeManifestPublish?, protectedIds?)`
  forwards the protected set to its cleanup call, so a capture during restore cannot evict the
  pre-operation checkpoint or the target before pending clears.

## Maintenance notes

- No `record` types (the build guard rejects the generated `System.Type`); all model types are
  sealed classes with constructor + get-only properties.
- `RestoreResult.PublishedId` is the published post/recovered checkpoint id; recovery uses the
  pre-operation id as a fallback when its capture is a no-op, so the renderer never reads a null id.
- Fault injection for the three interruption points goes through `Apply`'s `beforeFirstReplacement`,
  `afterFirstReplacement`, and `beforePostCheckpoint` hooks; `Recover` takes `beforeFirstReplacement`.
- Dry-run never takes the writer lock and writes nothing (`Plan` is read-only).
