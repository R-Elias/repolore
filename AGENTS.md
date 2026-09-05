# Agent Instructions

This repository uses RepoLore.

RepoLore is the repository’s operational memory for humans and coding agents. Before exploring, modifying, reviewing, or auditing this repository, read:

1. `_repolore/method.md`
2. `_repolore/root.md`, if it exists
3. the relevant RepoLore files for the area you are working on
4. `_repolore/product/product.md`, `_repolore/product/invariants.md`, and `_repolore/roadmap/roadmap.md` when planning or building RepoLore v1

Use RepoLore before broad source-code exploration.

When you make a meaningful change, update RepoLore if the change affects behavior, architecture, responsibilities, dependencies, conventions, important paths, or maintenance assumptions.

RepoLore is not exhaustive documentation. It is the working memory of the repository: what a competent maintainer should know before changing the code.

RepoLore does not replace the code. The code remains the source of truth. If RepoLore conflicts with the code, trust the code and update RepoLore.

The method and PowerShell tools currently describe the alpha layout. The v1 roadmap supersedes that layout for future implementation; do not migrate or regenerate knowledge merely because the plan changed. For planned short-term knowledge behavior, read `_repolore/product/sessions.md`. Sessions are explicitly selected local working knowledge, not a replacement for required durable updates; session-aware CLI support is not implemented yet. Custom planning areas under `_repolore/product/` and `_repolore/roadmap/` are authored directly.
