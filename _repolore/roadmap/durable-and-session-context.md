# Durable and session context — package 04

Read-only knowledge context: the `path`, `context`, and `tree` commands and the pure Core
selection/estimation that drive them. Durable path notes, alpha `tree/` legacy copies, and
explicitly selected session notes are read into one budgeted result; nothing is written.

## Folder layout

```text
src/RepoLore.Core/Context/        (RepoLore.Core.Context)
  SessionId.cs                    session-id validation (portable component, reserved names)
  KnowledgeNodePath.cs            --node path classification (durable/session/history/invalid)
  ContextModel.cs                 ContextRequest, ContextCandidate, ContextSelection
  ContextSelector.cs              selection order + canonical-path dedup
  ContextEstimate.cs              ContextBlock, ContextOmission, BlockEstimator
  NoteContent.cs                  BOM/UTF-8/CRLF/trim normalization + empty-marker check
  NoteReadResolver.cs             sparse-tree vs alpha tree/ preference and conflict rule
src/RepoLore.Cli/
  CommandLine.cs                  explicit option parser
  RepositoryRoot.cs               _repolore/ discovery (--repo-root or upward walk)
  KnowledgeReader.cs              reads a candidate (alpha-aware) via RepositoryPathResolver
  ContextCommand.cs, PathCommand.cs, TreeCommand.cs
  Output.cs                       text and JSON rendering (Core Json writer)
  ExitCodes.cs, UsageException.cs, Finding.cs
```

Core's `Context/` depends only on `Mapping/` (for `KnowledgePathMapper`). The CLI orchestrates
and reads files; Core stays pure.

## Model

- `SessionId.IsValid(id)` — one portable directory component: ASCII letters/digits/`_`/`-`,
  max 100 chars, first char alphanumeric. Rejects Windows reserved device names
  (`CON`/`PRN`/`AUX`/`NUL`, `COM1`–`COM9`, `LPT1`–`LPT9`) case-insensitively. No implicit
  current/latest session anywhere.
- `ContextSelector.Select(request)` — order is requested method → durable
  root/ancestors/target (when a source target is present) → selected session root → repeated
  `--node` in argument order. Deduplicated by canonical physical path (`sparse-tree/` for
  mapped notes), first position kept. `--node` alone reads only explicit nodes (plus method if
  requested). A source target maps to `root.md` plus one note per ancestor directory; the
  alpha legacy path for each mapped note is the same note under `tree/`.
- `KnowledgeNodePath.Classify(path)` — `--node` must be a `.md` note under `_repolore/` with
  no `..`/empty/backslash segments and never under `_repolore/.history/`. A `sessions/<id>/`
  path extracts its id; the selector rejects it unless it matches `--session`.
- `NoteContent.TryNormalize(bytes)` — removes a UTF-8 BOM, decodes strictly (invalid UTF-8 is
  a finding, not replacement text), normalizes CRLF to LF, trims outer whitespace. A note
  equal to `<!-- repolore:empty -->` (or blank) is empty.
- `NoteReadResolver.Resolve(primary, legacy)` — prefer the non-empty `sparse-tree/` copy, fall
  back to the non-empty alpha `tree/` copy, and report a conflict when both are non-empty and
  differ. Applies only to mapped directory notes; `root.md`, `method.md`, session notes, and
  `--node` paths are read literally.
- `BlockEstimator` — each included note renders to one canonical block
  `---\n## {repoRelativePath} [{scope}]\n\n{text}\n` (scope is `durable` or `session:<id>`,
  `/` separators). Charge is `ceil(String.Length / 4)` (UTF-16 code units), identical for
  text and JSON. Select in order; skip a non-fitting note and continue. Omission metadata is
  reported separately and never charged. `--strict` returns the findings exit code when
  anything was omitted for budget.

## Exit codes

`0` success; `1` findings (missing note/session, invalid UTF-8) or `--strict` budget omission;
`2` usage error (no selector, invalid `--session`, cross-session/history node, non-positive
budget); `3` conflict or IO/failure. Reads and previews write nothing (invariant 8).

## Maintenance notes

- Shipped projects must not use `record` types: the compiler emits `System.Type` in the
  generated `EqualityContract`, which the runtime-boundary build guard rejects. Use sealed
  classes with a constructor and get-only properties.
- `NoteContent` uses `UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes:
  true)`; the strict decode is the invalid-UTF-8 gate. BOM removal and CRLF normalization are
  hand-written, not `Encoding.GetString` span overloads.
- `tree` skips `sessions/` and `.history/` only at the `_repolore/` level; `--start
  _repolore/sessions/` lists session IDs without recursing, and `--start` into one session
  lists its notes.
- Selection, estimation, session-id validation, and alpha resolution are all pure and unit
  tested in `tests/RepoLore.Core.Tests/Context/`; end-to-end behavior is exercised by
  `tests/RepoLore.Cli.Tests/DurableSessionContextTests.cs` against the frozen fixtures.
