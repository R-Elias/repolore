# Unreleased (next version)

Draft notes for changes after `1.0.0-rc.2`. At the 1.0.0 cut, write `1.0.0.md` from the `1.0.0-rc.1` and `1.0.0-rc.2` notes plus the entries below (the release workflow refuses a version without its own notes file).

Settled before 1.0.0 because changing them later would change results scripts rely on (review of the field-test friction list, 2026-10-25).

- **Changed:** `tree` lists only Markdown notes and the folders that lead to one, each folder followed by its contents. It no longer lists other files, symlinks, folders without a note or `repolore.json`, and no longer lists all of a folder's children before any grandchild.
- **Changed:** checkpoint times have six fractional digits instead of seven, which Python before 3.11 could not parse. Older checkpoints are shown the same way.
- **Changed:** a `.repoloreignore` or `history.exclude` rule ending with a space or tab is invalid (`policy-error`). It used to match nothing, silently. Lines holding only whitespace are blank.
- **Changed:** `migrate --check --dry-run` is a usage error; use one of them.
- **Fix:** `init --update-method --json` reported `"checkpointId": null`; it now gives the checkpoint holding the new method, and the text output names it.
- **Testing:** scenarios tagged `contract` pin these decisions, and the ones kept as they are: the context budget's order, ignored unknown `repolore.json` keys, and exit 3 for a `restore --path` outside coverage. Four reference stages were added (`s43`–`s46`).
