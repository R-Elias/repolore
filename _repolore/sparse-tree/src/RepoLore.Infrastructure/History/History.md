# History

Local recovery for `_repolore/`: checkpoint capture, retention, restore, `init`, `init --update-method` and `migrate`. Recovery protects only states seen at a successful checkpoint; it is not a backup, so documented backups come before any destructive operation.

## Layout of `_repolore/.history/`

Local and Gitignored: `objects/<sha256>` (file contents, addressed by hash), `checkpoints/<16-digit id>.json` (manifests), `tmp/`, `write.lock`, and `pending.json` only while a mutation is in flight.

## Rules the code keeps

- **One writer:** every writing command opens `write.lock` exclusively. A second writer fails with `history-locked`. It is a small lock, not a background service. A file standing where `.history/` should be gets its own message and is never replaced.
- **Capture** (`CheckpointEngine`): reads the eligible files twice, storing objects on the first pass. If the two inventories differ it fails with "files changed while capturing; retry after pausing edits". A state identical to the latest checkpoint (same scope) is a no-op and gets no id. A checkpoint larger than `history.maxBytes` is refused. The manifest is published after its objects.
- **Retention** (`HistoryCleanup`): over budget, it evicts the oldest manifests first, never the newest and never a protected id, then deletes objects no remaining manifest references. A failed deletion is reported, not fatal.
- **Objects:** storing a hash that already exists verifies the stored bytes; a corrupt object stops the run with a message that names the remedy (delete that object and retry).
- **Preserve before mutation:** `restore`, `init --update-method` and `migrate` first capture a pre-operation checkpoint and protect it from retention, so the undo state survives. If that capture fails, nothing is changed. Then they write `pending.json` (temporary file, then rename), apply, capture again and clear it.
- **Interrupted operations:** a leftover `pending.json` makes every other mutation fail with `history-pending` and the pending id; `repolore restore <that id>` finishes the interrupted operation. `operation-interrupted` errors carry the same id.

## Testing seams

The engines take an injected `Clock` and a `deleteFile` function, and `Capture` has hooks before verification and before publishing the manifest. The tests use them for deterministic timestamps and failure injection; keep them when refactoring.

Full contracts: [snapshot capture](../../../../roadmap/snapshot-capture.md), [retention and mutation ownership](../../../../roadmap/retention-and-mutation-ownership.md), [restore and recovery](../../../../roadmap/restore-and-recovery.md), [initialization and method updates](../../../../roadmap/initialization-and-method-updates.md), [alpha migration](../../../../roadmap/alpha-migration.md).
