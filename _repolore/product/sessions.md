# Short-Term Knowledge — Sessions

Status: **planned for v1**, extending the durable knowledge model. Session-aware CLI behavior and checkpoint recovery are not implemented by the alpha scripts. See [roadmap](../roadmap/roadmap.md), [product direction](product.md), and [invariants](invariants.md).

## Three different responsibilities

| Area | Purpose | Git policy |
|---|---|---|
| `root.md`, `sparse-tree/`, and custom long-term areas | Established knowledge for future maintenance | Reviewed and committed by default |
| `sessions/<session-id>/` | Working findings, uncertainty, attempts, and handoff notes | Local; Gitignored and not committed |
| `.history/` | Recoverable versions of eligible knowledge files, including sessions | Local; Gitignored |

Sessions contain current working knowledge. Recovery history contains previous file states. Adding sessions does not replace or duplicate the durable tree.

## Folder contract

```text
_repolore/sessions/
  2026-09-05-payment-retry-a7c2/
    root.md
    investigation.md
    decisions-pending.md
    findings/
      retry-path.md
```

Agents create folders and edit Markdown directly; no session service or database. Choose a portable unique folder name and pass it explicitly when handing work over. The session `root.md` records the objective, status, findings with uncertainty, and next steps. Additional nodes and subfolders are freely authored. Resuming a session means deliberately choosing its existing ID, not implicitly taking the newest folder.

## Reading and authority

Proposed v1 commands:

```text
repolore context --session 2026-09-05-payment-retry-a7c2
repolore context src/payments/ --session 2026-09-05-payment-retry-a7c2
repolore context --session 2026-09-05-payment-retry-a7c2 --node _repolore/sessions/2026-09-05-payment-retry-a7c2/investigation.md
```

The first reads the selected session root. The second reads durable path context then that root. Other session notes are read only when explicitly requested. All selected content shares one context budget and reports omissions; output labels durable and provisional session sources separately.

Ordinary context and tree listings exclude sessions. Explicit listing through `tree --start _repolore/sessions/` can discover session IDs without loading their contents. No persisted active-session selection, cross-session implicit reads, or automatic link traversal. Missing sessions fail clearly and are never created by a read.

Session claims do not override established guidance. Verify a finding before promoting it. Promotion means incorporating a concise, evidence-backed note into the appropriate durable area through normal review; it is not an automatic move of the entire folder. Committed guidance must remain understandable without a local session folder. Session notes never replace required durable updates after meaningful changes.

## Recovery and cleanup

Add `_repolore/sessions/` and `_repolore/.history/` to Git ignore rules before using sessions. Existing tracked files must be explicitly untracked; ignore rules alone do not remove them from Git. This is a repository workflow convention, not an access-control boundary or a guarantee against forced commits.

Checkpoint before and after editing session knowledge. All eligible session Markdown shares the normal configurable 200 MiB history budget, regardless of which session is selected for context. `history.exclude` can opt paths out. Only states observed at successful checkpoints can be recovered.

Keep cleanup manual in v1: mark work complete, promote durable findings, checkpoint, delete the intended session folder using ordinary filesystem tools, then checkpoint again. There is no automatic expiry or session-folder eviction. Deletion does not erase retained snapshots; history retention and explicit purge are separate. Cleanup without a prior successful checkpoint can lose uncaptured contents.

Sessions are local to a checkout and do not accompany a fresh clone or automatically move between machines. A clone without sessions must remain fully usable for durable knowledge. RepoLore does not synchronize sessions or prevent an external agent from transmitting content it reads.
