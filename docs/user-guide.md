# RepoLore user guide

> **Preview.** Version `0.1.0-preview.1`. The package is **unsigned** and has not been published to NuGet.org; it is not a certified or enterprise-ready release. Everything below runs on your machine only: the tool makes no network calls, sends no telemetry and starts no other programs. Package restore (NuGet) and any editor or coding agent you use are separate systems that RepoLore does not control.

RepoLore keeps operational memory for a repository as Markdown under `_repolore/`: what a maintainer or coding agent should know before changing an area. You and your agents edit the Markdown directly; the `repolore` command reads it, checks it and protects it with local checkpoints.

## 1. Install

You need a **.NET 10 SDK** to install the tool and a **.NET 10.0.x runtime** to run it (the SDK includes one). Newer major runtimes are deliberately not used (`LatestPatch` roll-forward). NuGet installs neither. With a .NET 9 SDK, `dotnet tool restore` fails with `Settings file 'DotnetToolSettings.xml' was not found in the package`; that is expected, install the .NET 10 SDK. The tool does not care what language your repository uses.

**Recommended (team and CI): a pinned tool manifest.** Commit `.config/dotnet-tools.json` and an approved `NuGet.Config`:

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="approved-feed" value="https://your-internal-feed.example/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

```text
dotnet new tool-manifest
dotnet tool install RepoLore.Cli --version 0.1.0-preview.1 --local
dotnet tool restore
dotnet tool run repolore -- version
```

`<clear />` removes every other source, so only the approved feed is used. A local folder feed works the same way (`value="/path/to/folder"`). Commit no credentials. The manifest pins the exact version; updating RepoLore is a reviewed change to that file, and it never migrates your knowledge by itself.

**Alternative (single user, not exercised by the package tests):** `dotnet tool install --global RepoLore.Cli --version 0.1.0-preview.1 --add-source <feed>`, then run `repolore`.

All examples below write `repolore <command>`; with the manifest, run `dotnet tool run repolore -- <command>`.

## 2. Set up once

Run these in this order:

1. `repolore init` in the repository root. It creates `_repolore/repolore.json`, `method.md`, `root.md` and `sparse-tree/`, then takes the first checkpoint. Running it again changes nothing, and it never overwrites notes you wrote.
2. Add the two lines `init` prints to `.gitignore`, **before** you create any session:

   ```text
   _repolore/sessions/
   _repolore/.history/
   ```

3. Commit `.gitignore`, `.config/dotnet-tools.json`, `NuGet.Config` and the new `_repolore/` files.

Create sessions only after `init`. A `_repolore/` directory that holds nothing but a session folder is treated as alpha knowledge, and `init` refuses it with a `migrate` message and changes nothing.

## 3. Daily use

| You want to | Run |
|---|---|
| Read what is known before changing `src/payments/` | `repolore context src/payments/` (root note plus each ancestor's note, in that order) |
| See which notes exist for a path | `repolore path src/payments/` |
| Read a specific custom note | `repolore context --node _repolore/architecture/overview.md` |
| List durable knowledge | `repolore tree` |
| Save a recoverable state | `repolore checkpoint` |
| Check structure, links and history | `repolore health-check` |

Path notes live in `_repolore/sparse-tree/`: the directory `src/payments/` maps to `_repolore/sparse-tree/src/payments/payments.md`, and the repository root maps to `_repolore/root.md`. Files get no notes of their own; a note exists only when it holds knowledge. Custom areas such as `_repolore/architecture/` are equally valid.

Add `--json` to any command for one machine-readable document on stdout. Exit codes: `0` success, `1` findings (or `--strict` omissions), `2` usage error, `3` operational, format, conflict or recovery failure.

A routine for one change: `repolore context <path>` → `repolore checkpoint` → edit code and notes → `repolore checkpoint` → `repolore health-check` → review everything in one Git diff. Reads (`context`, `path`, `tree`, `history`, `health-check`, `--dry-run`) never write anything.

## 4. Sessions: short-term, local, provisional

A session is a folder `_repolore/sessions/<session-id>/` for one investigation or handoff. Use a portable unique id such as `2026-10-23-retry-path-a1c4`, create the folder yourself, and put a `root.md` in it (objective, status, findings with their uncertainty, next steps).

```text
repolore context src/ --session 2026-10-23-retry-path-a1c4
repolore context src/ --session 2026-10-23-retry-path-a1c4 --node _repolore/sessions/2026-10-23-retry-path-a1c4/investigation.md
```

- You always pass the id. There is no "current" or "latest" session, and default `context`, `tree` and `health-check` never open session files. Session A cannot read session B.
- Session notes are **provisional**. They never override durable notes, and context output labels them `session:<id>`.
- **Promote a verified finding** by editing the right durable note in `_repolore/sparse-tree/` (or a custom area) with a short, evidence-backed statement. Do not copy the folder, and do not link to the session: `health-check` reports `session-link-in-durable-note` because committed guidance must make sense without local files.
- **Clean up explicitly:** mark the session complete in its `root.md`, `repolore checkpoint`, delete the folder with ordinary tools, `repolore checkpoint` again. Nothing expires automatically. Deleting a session does not delete its checkpoints.
- Sessions stay on your machine. A fresh clone has none and still works.

## 5. What is recoverable, provisional and committed

| State | Lives in | In Git? | Recoverable by `restore`? | Status |
|---|---|---|---|---|
| Durable notes: `root.md`, `sparse-tree/`, custom areas | `_repolore/` | Yes | Yes, from a checkpoint (and from Git) | Established guidance |
| `method.md`, `repolore.json`, `_repoloreignore` | `_repolore/`, repo root | Yes | Yes | Configuration |
| Session notes | `_repolore/sessions/<id>/` | **No** (ignored) | Yes, from a checkpoint | **Provisional** |
| Checkpoints | `_repolore/.history/` | **No** (ignored) | not applicable | Local recovery state, not a backup |
| Source code | anywhere else | Yes | **No**, RepoLore never touches it | Use Git |

**Files that belong in a pull request:** the durable notes you changed, `.gitignore`, `.config/dotnet-tools.json`, `NuGet.Config` and, when you intend it, `_repolore/method.md` or `repolore.json`, together with the code they describe. **Never** `_repolore/sessions/` or `_repolore/.history/`. Before opening a PR run `git status`: if a session or history path appears, the ignore entries are missing. An ignore rule does not untrack a file that was already committed; remove such a file from the index yourself.

## 6. Format and migration

`_repolore/repolore.json` holds `{"formatVersion":1}` and optional history settings. A format the tool does not know fails safely: `context`, `path`, `tree` and every command that writes refuse with an upgrade message (exit 3) and change nothing. Updating the tool never migrates knowledge.

Knowledge from the alpha PowerShell tools (no `repolore.json`, a `tree/` and/or `sparse-tree/`) is migrated only on request:

```text
repolore migrate --check      # exit 1 when a clean migration is needed; writes nothing
repolore migrate --dry-run    # shows the full plan; writes nothing
repolore migrate              # applies it
```

Before running `migrate` on knowledge you care about, **make your own backup outside `_repolore/`** (copy the whole `_repolore/` directory and commit or tag the repository). Differing copies of the same note are a conflict: `migrate` refuses, writes nothing, and leaves the choice to you; `repolore checkpoint` first protects every variant while you decide. After a clean migration, `restore` of the migration baseline puts back the old bytes and the missing marker. Once migrated, use only the commands in this guide: the alpha scripts (`sync-tree`, `sync-sparse-tree`) must not run on a v1 knowledge base.

`repolore init --update-method` replaces `_repolore/method.md` with the guidance shipped in the installed version, after preserving the old bytes.

## 7. Recovery

`repolore checkpoint` stores complete copies of every eligible file (all `_repolore/` Markdown including every session, `repolore.json`, `_repoloreignore`) and skips the call when nothing changed. Only states seen by a successful checkpoint can be recovered: edits between checkpoints collapse into one, a file created and deleted between two checkpoints is gone, and the oldest checkpoints are evicted when history exceeds `history.maxBytes` (200 MiB by default; the latest checkpoint is never evicted). Pause editing while a checkpoint runs; a file that changes mid-capture makes it fail with a retry message.

```text
repolore history                                   # checkpoints and the files each holds
repolore restore 2 --path <note> --dry-run         # preview: add, replace or delete
repolore restore 2 --path <note>                   # apply to one path
repolore restore 2                                 # apply a whole checkpoint within its coverage
```

Applying a restore first checkpoints the current state of everything it will change, so you can undo it with the checkpoint that `restore` reports. If a restore (or `migrate`, or `init --update-method`) is interrupted, the tool records a pending operation and refuses other writes with the recovery id; run `repolore restore <that id>` to return to the pre-operation state. `history`, `context` and `health-check` keep working meanwhile.

To stop protecting a file, add it to `history.exclude` in `repolore.json` or set `history.enabled` to `false`. That does not erase existing checkpoints. To purge all local history, delete `_repolore/.history/` with ordinary tools when `health-check` shows no pending operation; recovery of everything captured so far is then gone. Checkpoints are local copies on the same disk and do not replace Git or a backup.

## Where to learn more

The design contract and per-area notes are under `_repolore/` in this repository, starting at `_repolore/root.md`.
