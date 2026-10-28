# RepoLore.Core

Pure decisions over strings and models; it never touches the disk, so everything here is cheap to test with small in-memory inputs. Infrastructure supplies the files and applies the results.

## What lives where

- `Mapping/`: source directory to note path and back. `src/payments/` maps to `src/payments/payments.md` under `_repolore/sparse-tree/`; the repository root maps to `root.md`. A note name must match its directory, only `root.md` may sit at the top, and `.`, `..`, backslashes and colons are refused.
- `Context/`: which notes a `context` call reads and in what order: the method (only with `--include-method`), the root note, each ancestor directory's note down to the target, the selected session's root, then every `--node` in argument order. Duplicates are dropped. Path notes are optional; root, method, session root and `--node` notes are required. `_repolore/tree/` is read only as the alpha fallback, when the sparse-tree note is absent; if both exist and differ, the note is reported as a conflict. The token budget (8000 by default) and its omission order are frozen.
- `Policies/` and `Matching/`: the three policies are independent and must stay so. Source discovery (`.repoloreignore`, built-in and default exclusions) says what counts as source; Git tracking is not modelled here; history coverage says what a checkpoint captures. `RuleSet` is the matcher both use (`!` negation, a trailing `/` for directories, `**` segments). A rule that ends with whitespace is refused.
- `Configuration/`: `repolore.json` (`formatVersion`, `history.enabled`, `history.maxBytes` with a 200 MiB default, `history.exclude`). Unknown formats fail with their own error and nothing else runs.
- `Snapshot/`, `Restore/`: the checkpoint manifest and its codec, the diff between two states, the restore plan, and the pending-recovery record. These are the on-disk formats of the history; they are frozen with it, so change them only additively and never in a way that an older 1.x cannot read.
- `Migration/`: the alpha-to-v1 plan.
- `Health/`: finding IDs, the Markdown link check (percent-escapes are decoded before the file is looked up), and the `--explain` text.
- `Json/`: the hand-written JSON reader and writer. Field order in the output is fixed by the code that builds the object.

## Coverage, precisely

A checkpoint captures only regular files that are `_repolore/**/*.md` outside `_repolore/.history/`, plus `_repolore/repolore.json` and `.repoloreignore`. Directories, symlinks and other file types are never captured, whatever `history.exclude` says. Recovery therefore cannot bring back an image or a script placed under `_repolore/`.

See [paths and mapping](../../../roadmap/paths-and-mapping.md), [configuration and policies](../../../roadmap/configuration-and-policies.md) and [durable and session context](../../../roadmap/durable-and-session-context.md) for the full contracts.
