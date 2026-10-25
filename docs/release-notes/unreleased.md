# Unreleased (next version)

Draft notes for changes after `1.0.0-rc.1`. At the 1.0.0 cut, write `1.0.0.md` from the `1.0.0-rc.1` notes plus the entries below (the release workflow refuses a version without its own notes file).

Found by an autonomous field test of `1.0.0-rc.1` on 2026-10-24 (57 experiments on a purpose-built repository).

- **Fix:** `restore` named the checkpoint taken after the restore, which holds the restored state, so restoring it changed nothing. It now ends with the command that undoes it, for example `undo with 'repolore restore 2'`, naming the checkpoint taken before. `migrate` and `init --update-method` report their undo command the same way, and their JSON gains `recoveryId`, as `restore` already had.
- **Fix:** `init` run below a directory that already has `_repolore/` created a second knowledge base, which then hid the existing notes from every command run there. It now refuses (exit 2, `usage`); `--repo-root .` still creates a separate knowledge base on purpose.
- **Fix:** `health-check --explain` said `covered` for notes while `history.enabled` was `false`. It now says `not covered` and why.
- **Fix:** `health-check` reported a link written with escapes, such as `Design%20Notes.md`, as broken although the note exists. Link targets are now decoded first; an encoded `..` is still caught as an escape.
- **Fix:** `--quiet` now silences `path` and `tree` too, as it already did the other commands.
- **Changed:** when a stored copy or a checkpoint file in `_repolore/.history/` is corrupt, the message that blocks writes now names the way out: delete the stored copy (the next checkpoint stores a fresh one), or move the checkpoint file aside (only that checkpoint is lost).
- **Changed:** the method that `init` writes says that targets are relative to the repository root, that custom-area notes are read with `--node`, and how the context budget works. Existing knowledge bases keep their method until `init --update-method`.
- **User guide:** it documents every option, the context budget, how targets resolve, the rule syntax of `.repoloreignore` and `history.exclude` and how it differs from `.gitignore`, the full `repolore.json`, session id rules, what the link check covers, letter case, what disabled history blocks, how to get out of corrupt history, and what a teammate runs after cloning.
