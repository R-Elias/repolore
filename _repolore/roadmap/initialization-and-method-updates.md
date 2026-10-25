# Initialization and method updates — package 08

`init [--update-method]` creates the missing entry points, configuration marker, and canonical
tree, then checkpoints. It builds on packages 05–07 (capture, retention, pending recovery); see
[snapshot-capture](snapshot-capture.md) and [restore-and-recovery](restore-and-recovery.md).

## Folder layout

```text
src/RepoLore.Infrastructure/History/
  InitEngine.cs                              InitDirKind + InitException + InitIncompleteException + InitResult + InitEngine
src/RepoLore.Cli/
  Commands/InitCommand.cs
  Embedded/EmbeddedTemplates.cs              v1 method and root templates as const strings
```

The templates ship as two `const string` values (`EmbeddedTemplates.Method`/`Root`) written
directly in code. A plain constant is used rather than an embedded resource because the build guard
forbids `System.Reflection` (no `Assembly.GetManifestResourceStream`).

## Classification

`InitEngine.Classify()` (read-only) decides `_repolore/`'s state: `Absent` (no directory), `Empty`
(no authored content; a lone `.history/` is tool-temporary and counts as empty), `Alpha` (authored
content but no `repolore.json` marker), or `V1` (marker parses as format 1; invalid/malformed or
higher-version markers throw). `init` refuses `Alpha` with a migrate instruction and never writes
into it. The CLI classifies before acquiring the writer lock so an alpha directory is never touched.

Without `--repo-root`, `InitCommand` first looks upward from the current directory the way every
other command discovers its root (`RepositoryRoot.FindEnclosing`). If a parent directory holds
`_repolore/`, init refuses with a usage error (exit 2) instead of creating a nested knowledge base
that every command run there would then use, hiding the enclosing notes (field-test finding E51,
fixed 2026-10-25). `--repo-root` is the deliberate override.

## Init flow

- Fresh (`Absent`/`Empty`): publish `_repolore/repolore.json` (`{"formatVersion":1}`) atomically
  (staged under `.history/tmp/`, then renamed) before any authored template, so a crash leaves no
  authored files and retry reclassifies cleanly. Then create only missing `method.md` (embedded v1
  guidance), `root.md` (placeholder), and the empty `sparse-tree/` directory; no source inspection,
  no empty nodes, no sessions, no `.gitignore` rewrite. Then capture the first checkpoint.
- Existing `V1`: create only missing files; never overwrite a present `root.md`, `method.md`, or
  any custom/session note. The checkpoint is a no-op when nothing was created.
- First-capture failure throws `InitIncompleteException` carrying the created paths; nothing is
  deleted and the marker remains, so re-running completes safely.
- `history.enabled=false`: files are still created, the checkpoint is skipped, and the result flags
  `HistoryDisabled` so the CLI states that protection is disabled.

## Method update transaction

`--update-method` runs only on an existing `V1` install. It requires enabled history and no pending
recovery, computes the current `method.md` hash vs the embedded template hash, and short-circuits as
a no-op when equal. Otherwise it reuses the package-07 pending mechanism unchanged: capture the
pre-operation checkpoint, write `pending.json` (target/pre-op both set to the pre-operation id, plus
the single `_repolore/method.md` before/after entry), stage-and-rename the new method bytes, capture
the post-checkpoint with the pre-operation id pinned, then clear pending. `InitResult.RecoveryId` carries
the pre-operation id; the CLI prints `updated _repolore/method.md; undo with 'repolore restore <id>'`
and JSON `recoveryId`. An interruption surfaces
`InitException` with the recovery id; the existing `restore <pre-operation-id>` route recovers the
old bytes because `PendingCodec`/`RestoreEngine.Recover` are format-agnostic.

## Maintenance notes

- `Encoding.UTF8.GetBytes(string)` is avoided in shipped code (it trips the build guard the same way
  `GetByteCount` does); template hashes and writes round-trip through `File.WriteAllText` +
  `File.ReadAllBytes` instead.
- No `record` types; all model types are sealed classes.
- This package left the checkout's `method.md` copies alone and shipped only `EmbeddedTemplates.Method`.
  The checkout's own copies were replaced on 2026-10-24 by running `init --update-method` with the
  published tool, then copying the result to the repository-root copy ([dogfood and preview](dogfood-and-preview.md)).
- On 2026-10-25 the method gained a paragraph on root-relative targets, `--node` for custom areas and
  the context budget (field-test finding E57). The checkout's copies were updated with the source
  build's `init --update-method` (pre-operation checkpoint 37), and reference stage
  `s42-method-guides-context` holds the new bytes for `init-reproduces-the-adoption`. A method change
  needs a new reference stage like this one, or that scenario fails.
