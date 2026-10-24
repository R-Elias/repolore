# Retention and mutation ownership — package 06

Bounded retained history against `history.maxBytes` (default 209715200) plus cross-process/crash
validation of the package-05 writer guard. No restore/update/migration handler is enabled yet; this
package only bounds history and proves the lock.

## Folder layout

```text
src/RepoLore.Infrastructure/History/
  HistoryCleanup.cs        CleanupResult + HistoryCleanup (byte counting, eviction, reclamation)
```

Retention lives in Infrastructure (physical IO); Core is unchanged. `CheckpointEngine` reuses the
existing `ObjectStore`/`CheckpointStore` and calls `HistoryCleanup.Clean` after publishing each
manifest. The writer guard (`HistoryWriterLock.cs`) is unchanged; package 06 only validates it.

## Behavior

- `CleanupResult` reports `RetainedBytes`, `Evicted`, `Reclaimed`, and `BudgetOk`.
- `HistoryCleanup.SnapshotBytes(manifest)` measures one snapshot's exact bytes (manifest JSON plus
  deduplicated objects). `Clean(maxBytes)` lists completed manifests, evicts oldest-first while
  always keeping the highest id (the latest), then reclaims objects no surviving manifest
  references. Shared objects are counted once and kept while any manifest references them.
- Exact byte sizes: object bytes equal the stored object file size (== `FileEntry.Size`); manifest
  bytes equal the UTF-8 length of the re-encoded manifest (== what `CheckpointStore.Publish` writes).
  `tmp/`, `write.lock`, and any pending control are never charged.
- Ordering: capture preflights that the new snapshot alone fits (`SnapshotBytes > maxBytes` fails
  before publishing), publishes the manifest, then evicts and reclaims. A failed delete is caught,
  leaves the store valid, and reports `BudgetOk=false`; cleanup retries on the next mutation, never
  on a read.
- `checkpoint` reports `evicted`, `reclaimed`, and `retainedBytes` (text and JSON) and prints a
  stderr warning when the budget was not achieved.

Exit codes: `checkpoint` still exits 3 on disabled history, lock contention, changed-during-capture,
corrupt store, or an over-budget snapshot; `history` and the read-only commands never take the lock
or clean up.

## Maintenance notes

- `HistoryCleanup` uses a hand-written UTF-8 byte counter (`Utf8Len`) because
  `Encoding.UTF8.GetByteCount(string)` trips the runtime-boundary guard and `JsonWriter` emits
  non-ASCII verbatim; object/manifest sizes are therefore exact, not UTF-16 char counts.
- Cleanup fault injection is a `Func<string,bool>` deleteFile delegate (default `File.Delete`).
  `CheckpointEngine` forwards it so tests can make a single delete fail and assert `BudgetOk=false`
  rather than a false "cleanup success".
- The latest id is the only protected manifest today; the restore-target pinning and pre-operation
  "undo pair" preflight from the roadmap will pass through the same eviction loop in package 07.
