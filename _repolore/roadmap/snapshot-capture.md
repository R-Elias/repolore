# Snapshot capture and publication — package 05

`checkpoint` and `history` backed by plain files, plus the basic exclusive writer guard used for
manifest ID allocation and publication. Retention/eviction and the cross-process/crash validation of
that guard are delivered in package 06 (see [retention-and-mutation-ownership](retention-and-mutation-ownership.md)).

## Folder layout

```text
src/RepoLore.Core/Snapshot/               (RepoLore.Core.Snapshot)
  SnapshotModel.cs                         HistoryVersion + FileEntry + CaptureScope + CheckpointManifest
  ManifestCodec.cs                         ManifestFormatException + ManifestId + ManifestCodec (strict decode)
  SnapshotDiffer.cs                        SnapshotDiff + SnapshotDiffer (no-op + add/change/remove classification)
src/RepoLore.Infrastructure/History/       (RepoLore.Infrastructure.History)
  HistoryStore.cs                          HistoryStoreException + ObjectStore (owns hashing) + CheckpointStore
  HistoryWriterLock.cs                     WriterLock + WriterLockException (.history/write.lock via FileShare.None)
  HistoryDiscovery.cs                      HistoryEnumerator (eligible-file walk) + HistoryConfigLoader (alpha default)
  CheckpointEngine.cs                      Clock + CheckpointResult + CheckpointEngine (two-pass capture + hooks)
src/RepoLore.Cli/Commands/                CheckpointCommand.cs, HistoryCommand.cs
```

Core stays pure (manifest model, codec, differ); Infrastructure owns all physical IO and reuses
`RepositoryPathResolver`/`HistoryCoveragePolicy`/`ConfigParser`/`Json`/`RuleSet`. Small types that
always travel together share a file (a model and its parts, an exception and its throwers, a clock
and its only consumer); a file only splits when it is a distinct, evolving responsibility.

## Model and storage

- Layout: `.history/objects/<sha256>`, `.history/checkpoints/<monotonic-id>.json`,
  `.history/tmp/`, `.history/write.lock` (and later `.history/pending.json`). Objects are
  immutable and published before the manifest; the manifest is published last via a
  same-directory temp-file rename.
- Each manifest records `historyVersion` (1), `id`, `timestamp`, `knowledgeFormat`,
  `scope {historyEnabled, maxBytes, exclude[], repoloreJson}`, and sorted
  `files[{path,hash,size}]`. `repoloreJson` is `present`/`absent`/`excluded`, so an eligible
  absence is distinct from an excluded/uncaptured one.
- IDs increase under the write guard (`previous highest + 1`), independent of clock changes.
  No mutable "latest" pointer: the highest valid completed ID determines order.
- Capture streams original bytes to a temp object while hashing, validates existing objects
  before reuse, then runs a second inventory/hash pass and fails without publishing if anything
  changed. No-op compares paths, hashes, and the full scope (including budget), never timestamps.

## Exit codes and behavior

- `checkpoint`: 0 (published or no-op), 3 on disabled history / lock contention /
  changed-during-capture / corrupt store. `history`: 0, 3 on a malformed completed manifest.
- Read-only `history` never takes the lock; incomplete files (non-`.json`, temp, short-id) are
  ignored, but a malformed completed manifest is an error, never silently skipped.

## Maintenance notes

- Shipped projects must avoid `record` types (the build guard rejects the generated
  `System.Type`). All manifest/scope/entry types are sealed classes with get-only properties.
- `RepoLoreConfig.HistoryExcludeRules` retains the raw `history.exclude` strings so manifests
  record the exact rules; a scope change is detected even when visible hashes match.
- JSON integer parsing is single-sourced in `Json/Json.cs` (`JsonNumbers.TryParseInteger`), used by
  both `ConfigParser` and `ManifestCodec`. Symlink/reparse-point detection is single-sourced in
  `RepositoryPathResolver.IsReparsePoint` (now public), used by `HistoryEnumerator`.
- The lock is a `FileStream(path, OpenOrCreate, ReadWrite, FileShare.None)` over `write.lock`;
  OS-held, so process death releases it. Do not infer ownership from file existence or delete by
  age (validated across processes in package 06).
- Fault injection goes through `CheckpointEngine.Capture`'s `beforeVerification` and
  `beforeManifestPublish` hooks (matching the guide's "inject failures after object write and
  before manifest publication").
