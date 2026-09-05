# RepoLore Tools

RepoLore tools are small helper scripts.

They are not the core of RepoLore. The core is the static knowledge base and the method.

The tools exist to make RepoLore easier for agents and developers to use.

## Available Alpha Tools

```text
tools/
  tools.md

  sync-tree/
    sync-tree.ps1

  sync-sparse-tree/
    sync-sparse-tree.ps1

  path/
    path.ps1

  context/
    context.ps1

  sparse-tree/
    sparse-tree.ps1
```

## Core Rule

Write knowledge only to:

```text
_repolore/tree/
```

Do not manually edit:

```text
_repolore/sparse-tree/
```

The sparse tree is generated from the full tree.

## Tool: sync-tree

Synchronizes `_repolore/tree/` with the repository structure.

It creates missing RepoLore nodes for folders and files.

It does not overwrite existing knowledge.

It does not delete existing knowledge.

Usage:

```powershell
pwsh tools/sync-tree/sync-tree.ps1
```

## Tool: sync-sparse-tree

Regenerates `_repolore/sparse-tree/` from `_repolore/tree/`.

Only non-empty knowledge nodes are copied.

Usage:

```powershell
pwsh tools/sync-sparse-tree/sync-sparse-tree.ps1
```

Run this after changing `_repolore/tree/`.

## Tool: path

Returns the RepoLore files that should be read for a repository path.

It returns paths, not contents.

Usage:

```powershell
pwsh tools/path/path.ps1 -TargetPath "src/services/payment/stripe.ts"
```

## Tool: context

Reads the relevant RepoLore files for a repository path and prints a combined context.

It skips empty nodes.

It stops before exceeding the token budget.

Usage:

```powershell
pwsh tools/context/context.ps1 -TargetPath "src/services/payment/stripe.ts" -BudgetTokens 8000
```

## Tool: sparse-tree

Displays the non-empty RepoLore knowledge tree.

Usage:

```powershell
pwsh tools/sparse-tree/sparse-tree.ps1
```

To display from a specific point:

```powershell
pwsh tools/sparse-tree/sparse-tree.ps1 -StartPath "src/services"
```

## Token Estimate

The alpha scripts estimate tokens approximately as:

```text
1 token ≈ 4 characters
```

This is intentionally simple. It is good enough for early agent usage.

## Design Principle

The tools should stay simple, deterministic, local, and easy to inspect.

They should help agents navigate RepoLore, not reason on behalf of agents.
