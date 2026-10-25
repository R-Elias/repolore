# Health checks and the final CLI contract — package 10

`health-check` observes and reports; it never repairs. Every command shares one error/exit-code
contract and one findings table. See [implementation guide](implementation-v1.md#10--finish-diagnostics-and-stable-cli-behavior)
for the pass/fail gates, [durable and session context](durable-and-session-context.md) for the
context findings, and [alpha migration](alpha-migration.md) for `migrate --check`.

## Folder layout

```text
src/RepoLore.Core/Health/                 Finding, FindingIds (catalog + blocking rule), MarkdownLinks, PolicyExplanation
src/RepoLore.Infrastructure/Health/       HealthChecker (orchestrator), FormatHealthCheck, NoteStructureCheck,
                                          LinkCheck, HistoryHealthCheck, HealthReport
src/RepoLore.Cli/Failures/                ErrorIds, CliFailureException
src/RepoLore.Cli/Arguments/CommandRules.cs   per-command option table (one place)
src/RepoLore.Cli/Rendering/               HealthRenderer, ErrorRenderer
src/RepoLore.Cli/Repository/FormatGate.cs    read-side refusal of malformed/unsupported repolore.json
```

`NoteStructureCheck` reuses package 09's `MigrationEngine.Inventory()` + `MigrationPlanner.Classify`
(read-only) rather than a second tree walker: conflicts, ambiguous names, and useful notes whose
source directory is missing all come from that one classification.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Completed; no findings |
| 1 | Findings that do not block use, `migrate --check` with migration needed, `context --strict` omissions |
| 2 | Usage error (unknown/duplicate/misplaced option, extra argument, invalid `--session`, bad `--explain` path) |
| 3 | Operational failure: format/policy/conflict/recovery state, IO, lock, refused mutation |

A finding is *blocking* (exit 3) when it means RepoLore cannot trust or safely interpret the state
(`FindingIds.IsBlocking`). All other findings exit 1. This is the single rule used by `context`,
`path`, and `health-check`.

## Findings table (frozen in `tests/RepoLore.Cli.Tests/Fixtures/expected-ids.tsv`)

| ID | Exit | Emitted by | Meaning |
|---|---|---|---|
| `missing-note` | 1 | health, context | Required `method.md`/`root.md` absent, or an explicitly requested note absent |
| `invalid-utf8` | 1 | health, context | A note is not valid UTF-8 |
| `invalid-note-name` | 1 | health | A path note does not match its directory (`foo/bar.md`) |
| `broken-link` | 1 | health | Relative inline link whose target does not exist or escapes the repository |
| `orphan-path-note` | 1 | health | A non-empty path note whose source directory does not exist |
| `invalid-session` | 1 | health, context | Selected session missing, or its `root.md` missing |
| `session-link-in-durable-note` | 1 | health | A durable note links into `_repolore/sessions/` (portability) |
| `migration-needed` | 1 | health | No marker and nonempty `_repolore/`: alpha format |
| `malformed-format` | 3 | health, errors | `repolore.json` is not valid JSON / missing `formatVersion` / duplicate key |
| `unsupported-format` | 3 | health, errors | `formatVersion` is not the supported version (higher: upgrade instruction) |
| `policy-error` | 3 | health, errors | Invalid `history.*` value or rule, or invalid `.repoloreignore` rule (with line number), or a leftover preview-named `_repoloreignore` (rename it). An invalid ignore rule blocks no other command (decision of 2026-10-24) |
| `conflicting-alpha-notes` | 3 | health, context, path | `tree/` and `sparse-tree/` copies differ |
| `history-unavailable` | 3 | health | `.history` exists but is a file, a symlink, or unreadable |
| `history-corrupt` | 3 | health | Malformed completed manifest or `pending.json`; a referenced object is missing or fails its hash |
| `history-pending` | 3 | health, errors | An interrupted operation is pending (carries the recovery ID) |

Not findings: a missing optional path note, missing `.history/` in a fresh clone, an empty alpha
marker, session contents when no session is selected.

Operational-only error IDs: `usage` (2), `history-locked`, `history-disabled`, `history-failure`,
`operation-interrupted` (carries `recoveryId`), `init-incomplete`, `alpha-needs-migration`,
`migration-blocked`, `restore-plan-invalid`, `unknown-checkpoint` (package 12.5), `io-error`, `internal-error` (all 3). The format
errors reuse the finding IDs `malformed-format`/`unsupported-format`/`policy-error`, and a pending
record reuses `history-pending`.

IDs are public API: add new ones, never rename. Update the fixture first, then the code.

## JSON and diagnostics

- With `--json`, stdout is exactly one JSON document and never carries progress or stack traces.
  Success shapes are per command (`version`, `notes`, `included/omitted/findings`, `entries`,
  `checkpoints`, `noOp`, `created`, `migrationNeeded/blocked/mappings`, `healthy/findings/explain`).
- A failure (exit 2 or 3 without a result document) writes
  `{"error":{"id":"…","message":"…","recoveryId":null|N}}` to stdout and the same concise message to
  stderr (`repolore: …`, plus `repolore: recover with: repolore restore N` when applicable).
  Usage errors also print the one-line usage first. Without `--json` only stderr is written.
- Commands whose result document already carries the failure (`health-check`, `context`,
  `migrate --check/--dry-run` with conflicts) return exit 1/3 with that document and no envelope.
- `health-check --json`: `{"healthy":bool,"findings":[{id,path,message}],"explain":null|{…}}`;
  findings sorted by path, then id, then message.
- Unexpected exceptions map to `internal-error` (message only, no stack trace).

## Command-line rules

`CommandRules` lists the options each command accepts; `--repo-root`, `--json`, `--quiet` are
common (except `version`, which takes only `--json`). Unknown options, options a command does not
accept, a repeated singleton option, and extra positionals are usage errors. `--node` is the only
repeatable option. `--help` and `version` need no repository. `migrate` takes `--check` or
`--dry-run`, not both (a usage error since 2026-10-25). `--quiet` suppresses all normal stdout
of every command that accepts it (`path` and `tree` ignored it until 2026-10-25); diagnostics stay
on stderr.

Read commands (`path`, `context`, `tree`) call `FormatGate`: if `repolore.json` exists but is
malformed or unsupported they exit 3 rather than reading notes. `history` and the recovery route of
`restore` deliberately do not require a valid marker, so a damaged marker never blocks recovery.

## `health-check [--explain <path>] [--session <id>]`

Checks, in order: format marker and `.repoloreignore` (rules validated; a leftover `_repoloreignore` is reported); required notes; alpha
inventory classification; local links in durable Markdown (everything under `_repolore/` except
`sessions/` and `.history/`); the explicitly selected session (links and layout); history
(`pending.json`, completed manifests, every referenced object's hash). History directory absent is
normal. Nothing is created, rewritten, or cleaned; no lock is taken.

`--explain` evaluates a repo-root-relative path with the same matcher the policies use: source
discovery (hard exclusion → default → user rule, with the rule text) and history coverage
(eligibility, then `history.exclude`). It never reports Git tracking; it says so and points to Git.
It is skipped (and a policy finding explains why) when `repolore.json` or `.repoloreignore` is invalid.
With `history.enabled` false every eligible path is `not covered`, with that reason (it said
`covered` until 2026-10-25).

### Link subset

Inline `[text](target)` and `![alt](target)` links only. Skipped: targets with a URL scheme
(`http:`, `mailto:`, …), fragment-only, root-absolute (`/…`), links inside fenced code blocks
(``` or ~~~) and inline code spans. `<target>` and `target "title"` forms are read; the `#fragment`
is stripped, valid `%XX` escapes are decoded as UTF-8 (since 2026-10-25; an encoded `..` is still an
escape, and a target whose escapes are not valid UTF-8 is kept as written), and only file/directory
existence is checked. Limits: no nested parentheses in targets, no reference-style links, no HTML. A link from a selected session to
another session is neither followed nor validated.

## Known limits

- Health cannot certify semantic freshness; it only checks structure, links, and recovery state.
- Default health lists the entry names inside `_repolore/sessions/` (via the migration inventory)
  but never opens session files.
- A run with no `_repolore/` directory exits 2 like every other repository command.
- Corrupt/missing target objects and budget failures surface as the generic `history-failure`; an
  unknown restore ID is `unknown-checkpoint` and `--path` scope explanations are
  `restore-plan-invalid`. Finer IDs can be added without renaming existing ones.
- `init` refuses a markerless nonempty `_repolore/` with the `alpha-needs-migration` ID, but the
  message covers both causes (alpha knowledge, or session folders created before `init`) and gives
  the move-aside recipe for the second; the classification rule itself is unchanged. `migrate --json`
  carries an additive `markerWritten`; `history` prints `no checkpoints` in text mode when empty.
