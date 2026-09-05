# RepoLore — Desired Result

Status: product direction for the planned v1; features below are not claims about the current alpha scripts. Implementation contract: [roadmap](../roadmap/roadmap.md). Execution sequence: [first-release work packages](../roadmap/implementation-v1.md). Absolute constraints: [invariants](invariants.md).

## Philosophy

RepoLore is an open-source operational memory for developers and coding agents, maintained by a single developer. “Product” means a usable, dependable tool, not a sales platform. It should be simple enough to understand, maintain, approve, and recover without a specialist team.

Knowledge stays in the repository as readable Markdown. Record what a competent maintainer needs before making a change: constraints, reasons, pitfalls, and relationships. Keep useful knowledge sparse; a complete tree of empty files is clutter. RepoLore informs humans and agents; it does not act on their behalf or call an AI service.

Privacy is architectural: all built-in work is local, with no account, telemetry, remote processing, or network dependency. Banks and administrations should be able to approve and redistribute an ordinary package through existing infrastructure. This does not imply a hosted enterprise platform or a certification claim.

Users own the knowledge and can edit it with ordinary tools. Path notes live in one authoritative `sparse-tree/`; custom areas such as architecture and domain knowledge are equally valid. Git review remains the normal review experience. Short-term knowledge lives separately in Gitignored `sessions/<session-id>/` folders: provisional findings, attempted approaches, open questions, and handoff notes. Agents explicitly select a session and promote verified findings into durable knowledge through review. See the [session contract](sessions.md). Local checkpoint history protects eligible untracked knowledge, including sessions, with a generous configurable budget and clearly stated capture limits.

## Desired daily experience

1. Security/platform team reviews a small release bundle and approves a version in its internal NuGet feed, including Artifactory.
2. Developer restores the repository's pinned tool version; no registration or service setup.
3. `init` creates a minimal knowledge base and first checkpoint, preserving existing content.
4. Developer or agent reads durable context, creates or explicitly resumes a local session, checkpoints, and edits Markdown directly.
5. A second checkpoint records the observed edits; health-check reports structural issues with actionable paths.
6. Verified session findings are incorporated into durable knowledge. Code and durable knowledge changes are reviewed together as ordinary diffs; session folders remain local.
7. History shows recoverable versions; restore previews its effect and preserves current content before applying.

Checkpointing is explicit, not continuous surveillance of editor saves. No watcher runs in the background. Read-only commands remain read-only.

## Release objectives

| Release | Desired result | Deliberately outside scope |
|---|---|---|
| Current alpha | Demonstrate the method with Markdown and PowerShell helpers; preserve existing knowledge while preparing the new contract. | Treating the old full mirror as the future architecture. |
| v1 preview | Validate one knowledge tree, custom areas, local sessions with explicit context selection, deterministic context, checkpoint recovery, explicit migration, and installation of one NuGet tool package. | Plugins, extra package ecosystems, commercial infrastructure. |
| v1.0.0 | A dependable core CLI distributed as `RepoLore.Cli`, usable through approved/offline feeds with a small deterministic test suite, release verification, and clear session handoff, promotion, and recovery instructions. | Separate public Core library package, daemons, semantic indexing, cloud services. |
| v1.x | Stabilize the same workflow: fixes, compatibility, understandable diagnostics, and improvements justified by actual use. | Mandatory broad experiments or speculative frameworks. |
| Future releases, unnumbered | Consider additional distribution or integrations only when a concrete need justifies their maintenance cost. Official plugins, if introduced, remain network-free. | Assuming third-party plugins are covered by RepoLore's privacy guarantee. |

Success means less repeated investigation and fewer lost knowledge edits, with little maintenance overhead. A small comparison of real tasks can help assess usefulness; do not claim numerical improvements without measuring them or make a research program a prerequisite for an open-source release.
