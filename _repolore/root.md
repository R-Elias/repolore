# Root — repolore-poc

This repository is the proof of concept of the RepoLore method.

The repo content is the source of truth: `method.md` (the method), `tools/` (PowerShell alpha tools), `AGENTS.md`, `LICENSE`. `_repolore/` is an instance of the RepoLore knowledge base used on this repo exactly as a client would use it — it is independent of the repo's own content.

## What this repository is

- A proof of concept proving that a static, repo-native knowledge base (`_repolore/`) is usable by humans and coding agents.
- The origin of RepoLore v0: a cross-platform .NET 10 CLI that replaces the alpha PowerShell tools.

## Where to start

- Planning or building RepoLore v0 → read `_repolore/roadmap/roadmap.md`.
- Understanding the RepoLore method → read `method.md` at the repo root (copy in `_repolore/method.md`).
- Working with the alpha tools → read `tools/tools.md`.
- Working on this repo as an agent → read `AGENTS.md`.

## Key decisions

- The knowledge directory is `_repolore/`, not `.repolore/`, to avoid hidden-folder behaviors in file managers, globbing, and tooling.
- `_repolore/tree/` is gitignored (local working mirror with many empty nodes); `_repolore/sparse-tree/` is committed and carries the non-empty knowledge nodes.
- RepoLore v0 targets .NET 10 LTS.
- The alpha PowerShell tools are the reference semantics for the v0 CLI commands.
- `method.md` exists twice: at the repo root (content of the tool's repo) and in `_repolore/method.md` (client-style copy). Update both when the method changes.

## Known uncertainty

- None recorded yet. Keep `_repolore/roadmap/roadmap.md` current as decisions change.
