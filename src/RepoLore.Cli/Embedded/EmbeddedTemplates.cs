namespace RepoLore.Cli.Embedded;

internal static class EmbeddedTemplates
{
    internal const string Method = @"# RepoLore Method

RepoLore is a local, offline operational memory for this repository: plain Markdown that records
what a future maintainer or coding agent should know before changing an area - constraints,
reasons, pitfalls, relationships, and decisions. RepoLore informs; humans and agents act. It never
calls a network or an AI service.

## Knowledge areas

- `_repolore/method.md` - this method.
- `_repolore/root.md` - a task-oriented map: where to start for common kinds of work and which
  notes matter for each area.
- `_repolore/sparse-tree/` - authored path notes. Each source directory maps to one note named
  after it (`src/` -> `_repolore/sparse-tree/src/src.md`; the repository root maps to `root.md`).
  Only directories get notes; knowledge about a file belongs in its directory's note. Missing
  notes are normal; do not fill empty nodes just to look complete.
- Custom long-term areas (for example `_repolore/architecture/`) - equally valid authored
  knowledge.
- `_repolore/sessions/<session-id>/` - short-term working knowledge (findings, attempts, open
  questions). Local and Gitignored; never committed. A session is selected explicitly; its notes
  are provisional, not established guidance.
- `_repolore/.history/` - local checkpoint history for recovering previous states. Gitignored.

## Reading before work

Read `method.md`, then `root.md`, then the non-empty notes along the path to the target, and only
then inspect source. Prefer `repolore context <path>` and `repolore path <path>` over opening many
files by hand.

## Writing after work

Update RepoLore when a change affects operational memory: behavior, architecture, responsibilities,
dependencies, conventions, important paths, assumptions, or non-obvious decisions. Write at the
level where it helps future work; prefer concise operational knowledge over exhaustive summaries.

## Sessions

Create or resume a session by choosing its folder name explicitly under
`_repolore/sessions/<session-id>/`. Promote a verified finding into durable knowledge through
normal review; do not auto-promote or treat session notes as authoritative. Sessions stay local,
are never expired automatically, and do not accompany a fresh clone.

## Recovery

`repolore checkpoint` captures the current eligible state; `repolore history` lists checkpoints;
`repolore restore <id>` recovers a previous state within coverage. Only states observed at
successful checkpoints are recoverable. Read-only commands (context, path, tree, history, and all
previews) never write.
";

    internal const string Root = @"# Root

No operational knowledge has been recorded yet. Record a task-oriented map here: what this
repository is, its major areas, and where to start for common kinds of work. Keep it concise and
update it as real work touches the code.
";
}
