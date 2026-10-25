# RepoLore user guide

> **Version `1.1.1`.** It has **no author signature** (NuGet.org adds only its own repository signature), and it is not a certified or enterprise-ready release. Everything below runs on your machine only: the tool makes no network calls, sends no telemetry and starts no other programs. Package restore (NuGet) and any editor or coding agent you use are separate systems that RepoLore does not control.

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
dotnet tool install RepoLore --version 1.1.1 --local
dotnet tool restore
dotnet tool run repolore -- version
```

`<clear />` removes every other source, so only the approved feed is used. A local folder feed works the same way (`value="/path/to/folder"`). Commit no credentials. The manifest pins the exact version; updating RepoLore is a reviewed change to that file, and it never migrates your knowledge by itself.

**Alternative (single user, not exercised by the package tests):** `dotnet tool install --global RepoLore --version 1.1.1 --add-source <feed>`, then run `repolore`.

**Renamed in 1.1.1:** up to 1.1.0 the package was `RepoLore.Cli`; the command is still `repolore`. To move a repository's manifest to the new name, remove the old entry: `dotnet tool` accepts both packages in one manifest and then silently runs one of them for `repolore`. Run `dotnet tool uninstall RepoLore.Cli --local`, then `dotnet tool install RepoLore --version 1.1.1 --local`. Published `RepoLore.Cli` versions stay installable but are deprecated and receive no new versions.

**Without .NET: the native executable.** Since 1.1.0, the same tool also ships as a self-contained executable for Windows, Linux (glibc) and macOS, on x64 and arm64. It needs no .NET:

| With | Install | Pin a version |
|---|---|---|
| Script, macOS or Linux | `curl -fsSL https://repolore.org/install.sh \| sh` | `REPOLORE_VERSION=<version>` |
| Script, Windows | `irm https://repolore.org/install.ps1 \| iex` | `$env:REPOLORE_VERSION = '<version>'` |
| Homebrew | `brew install repolore-org/tap/repolore` | not offered |
| winget | `winget install repolore` (identifier `RepoLore.RepoLore`) | `--version <version>` |
| pipx or uv | `pipx install repolore` or `uv tool install repolore` | `repolore==<version>` |
| npm | `npm install -g repolore-cli` | `repolore-cli@<version>`, or a dev dependency |
| Download | the archives attached to the GitHub release | the release tag |

The scripts check the archive against the release's checksum file before installing it. `REPOLORE_INSTALL_DIR` changes where they install, and `REPOLORE_DOWNLOAD_BASE` points them at an internal mirror of the release files. npm installs a small Node.js launcher that starts the executable; every other channel installs the executable itself. The executables have **no code signature**: on macOS a browser download is blocked until you allow it and on Windows SmartScreen may warn, so prefer a package manager or the script. On npm, an unrelated package named `repolore` installs a command of the same name; do not install both globally.

All examples below write `repolore <command>`; with the manifest, run `dotnet tool run repolore -- <command>`.

## 2. Set up once

Run these in this order:

1. `repolore init` in the repository root. It creates `_repolore/repolore.json`, `method.md`, `root.md` and `sparse-tree/`, then takes the first checkpoint. Running it again changes nothing, and it never overwrites notes you wrote. Below a directory that already has `_repolore/`, it refuses (exit 2), because every command run there would then use the new knowledge base and hide the existing notes; pass `--repo-root .` when you do want a separate one.
2. Add the two lines `init` prints to `.gitignore`, **before** you create any session:

   ```text
   _repolore/sessions/
   _repolore/.history/
   ```

3. Commit `.gitignore`, `.config/dotnet-tools.json`, `NuGet.Config` and the new `_repolore/` files.

**A teammate who clones the repository** gets the committed notes, but no sessions and no checkpoints. They run `repolore init` once: it adds only what is missing and takes the first local checkpoint, so recovery works from then on.

Create sessions only after `init`. If you created a session folder first, `init` refuses (exit 3, nothing changed) because `_repolore/` already holds files but has no `repolore.json`. Move `_repolore/sessions/` aside, run `repolore init`, then move it back.

## 3. Daily use

| You want to | Run |
|---|---|
| Read what is known before changing `src/payments/` | `repolore context src/payments/` (the root note, then the note of each directory down to `src/payments/`, in that order) |
| See which notes exist for a path | `repolore path src/payments/` |
| Read a specific custom note | `repolore context --node _repolore/architecture/overview.md` |
| List durable knowledge | `repolore tree` (Markdown notes and the folders that hold them, each folder followed by its contents) |
| Save a recoverable state | `repolore checkpoint` |
| Check structure, links and history | `repolore health-check` |

Path notes live in `_repolore/sparse-tree/`: the directory `src/payments/` maps to `_repolore/sparse-tree/src/payments/payments.md`, and the repository root maps to `_repolore/root.md`. Files get no notes of their own; a note exists only when it holds knowledge. Custom areas such as `_repolore/architecture/` are equally valid.

**Targets** are paths relative to the repository root, written with `/`. `src/payments/` works from any directory inside the repository, because the tool finds the root by walking up to the nearest `_repolore/` (or takes it from `--repo-root <dir>`); from `src/`, `payments/` would mean a top-level `payments/` directory. `./`, `..`, `\` and absolute paths are refused (exit 2). `context <path>` never pulls in custom areas: add each note you need with `--node`, which can be repeated. Letter case follows your file system: on macOS and Windows a target in the wrong case still finds its note, on Linux it does not, so write paths in their exact case.

**Context budget.** `context` keeps its output within about 8000 tokens, estimated at 4 characters per token; `--budget-tokens <n>` changes the limit. Notes are taken in order, root first, and a note that does not fit is left out while the later ones are still tried. Each left-out note is named on stderr as `omitted: <note> (budget)` (in JSON, under `omitted`), and the exit code stays `0` unless you pass `--strict`.

| Option | Commands | Effect |
|---|---|---|
| `--json` | all | One machine-readable document on stdout, including for errors |
| `--quiet` | all except `version` | No normal output: only the exit code, and diagnostics on stderr |
| `--repo-root <dir>` | all except `version` | Use this repository root instead of searching upward from the current directory |
| `--node <note>` | `context` | Also read this note under `_repolore/`, after the path notes; repeatable |
| `--session <id>` | `context`, `health-check` | Include or check this session (section 4) |
| `--budget-tokens <n>`, `--strict` | `context` | Change the budget; exit `1` when a note was left out |
| `--include-method` | `path`, `context` | List or read `_repolore/method.md` first |
| `--start <dir>` | `tree` | List only below this directory under `_repolore/` |
| `--path <file>`, `--dry-run` | `restore` | Restore one file; preview without writing (section 7) |
| `--check` or `--dry-run` | `migrate` | Report or preview a migration without writing (section 6); not both |
| `--explain <path>` | `health-check` | Show which rules decide discovery and history coverage for a path (section 6) |
| `--update-method` | `init` | Replace `method.md` with the installed version's (section 6) |

`repolore --help` lists the commands. Exit codes: `0` success, `1` findings (or `--strict` omissions), `2` usage error, `3` operational, format, conflict or recovery failure.

`health-check` checks inline links and images with a relative target, such as `[text](../payments/payments.md)`, after decoding escapes such as `%20`. It does not check reference-style links (`[text][ref]`), links starting with `/`, web links, or anything in code spans and fenced blocks.

A routine for one change: `repolore context <path>` → `repolore checkpoint` → edit code and notes → `repolore checkpoint` → `repolore health-check` → review everything in one Git diff. Reads (`context`, `path`, `tree`, `history`, `health-check`, `--dry-run`) never write anything.

## 4. Sessions: short-term, local, provisional

A session is a folder `_repolore/sessions/<session-id>/` for one investigation or handoff. Use a portable unique id such as `2026-10-23-retry-path-a1c4`: 1 to 100 ASCII letters, digits, `-` and `_`, starting with a letter or a digit, and not a reserved Windows device name such as `CON`. Create the folder yourself, and put a `root.md` in it (objective, status, findings with their uncertainty, next steps).

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
| `method.md`, `repolore.json`, `.repoloreignore` | `_repolore/`, repo root | Yes | Yes | Configuration |
| Session notes | `_repolore/sessions/<id>/` | **No** (ignored) | Yes, from a checkpoint | **Provisional** |
| Checkpoints | `_repolore/.history/` | **No** (ignored) | not applicable | Local recovery state, not a backup |
| Source code | anywhere else | Yes | **No**, RepoLore never touches it | Use Git |

**Files that belong in a pull request:** the durable notes you changed, `.gitignore`, `.config/dotnet-tools.json`, `NuGet.Config` and, when you intend it, `_repolore/method.md` or `repolore.json`, together with the code they describe. **Never** `_repolore/sessions/` or `_repolore/.history/`. Before opening a PR run `git status`: if a session or history path appears, the ignore entries are missing. An ignore rule does not untrack a file that was already committed; remove such a file from the index yourself.

## 6. Format and migration

`_repolore/repolore.json` holds `{"formatVersion":1}` and optional history settings, shown here with their defaults except `exclude`:

```json
{
  "formatVersion": 1,
  "history": {
    "enabled": true,
    "maxBytes": 209715200,
    "exclude": ["_repolore/drafts/"]
  }
}
```

Other keys are ignored without a warning, so check the spelling: `repolore health-check --explain _repolore/root.md` shows whether history covers a note. A format the tool does not know fails safely: `context`, `path`, `tree` and every command that writes refuse with an upgrade message (exit 3) and change nothing. Updating the tool never migrates knowledge.

An optional `.repoloreignore` at the repository root holds source-discovery rules, in the same syntax as `history.exclude`. Today these rules only change what `health-check --explain` reports. An invalid rule is a blocking `policy-error` in `health-check` (exit 3, with its line number), but it blocks nothing else: reads and writes go on, because the rules never decide what history protects. The `0.1.0-preview.1` release named this file `_repoloreignore`. A leftover file with that name is a blocking `policy-error` too (`health-check` exits 3 until you rename it), and its rules are not read.

The rules look like `.gitignore` rules but differ in three ways:

- **Every rule is anchored at the repository root.** `*.pyc` matches only `x.pyc` at the top; write `**/*.pyc` for any depth. A leading `/` changes nothing.
- **`!` re-includes a path even below an excluded directory** (`generated/` then `!generated/schemas/`), which Git cannot do.
- **Only `*` and `?` (within one path segment) and `**` (a whole segment) are special.** Character classes (`[abc]`) and backslash escapes are refused, except `\#` and `\!` at the start. Spaces inside a pattern belong to it (`docs/Design Notes/`), but a rule ending with a space or tab is refused as invalid, since it could never match what you meant. A `#` after a rule is part of the pattern, not a comment; put comments on their own line.

One rule per line; empty lines and lines starting with `#` are skipped. A trailing `/` matches directories only, a rule that matches a directory also matches everything below it, and the last matching rule wins. Matching is case-sensitive. In `history.exclude`, each array item is one rule, and errors give its position (`rule #2`). `--explain` treats a path that does not exist as a file: add a trailing `/` to ask about a directory.

Knowledge from the alpha PowerShell tools (no `repolore.json`, a `tree/` and/or `sparse-tree/`) is migrated only on request:

```text
repolore migrate --check      # exit 1 when a clean migration is needed; writes nothing
repolore migrate --dry-run    # shows the full plan; writes nothing
repolore migrate              # applies it
```

Before running `migrate` on knowledge you care about, **make your own backup outside `_repolore/`** (copy the whole `_repolore/` directory and commit or tag the repository). Differing copies of the same note are a conflict: `migrate` refuses, writes nothing, and leaves the choice to you; `repolore checkpoint` first protects every variant while you decide. After a clean migration, `restore` of the migration baseline puts back the old bytes and the missing marker. Once migrated, use only the commands in this guide: the alpha scripts (`sync-tree`, `sync-sparse-tree`) must not run on a v1 knowledge base.

`repolore init --update-method` replaces `_repolore/method.md` with the guidance shipped in the installed version, after preserving the old bytes.

## 7. Recovery

`repolore checkpoint` stores complete copies of every eligible file (all `_repolore/` Markdown including every session, `repolore.json`, `.repoloreignore`) and skips the call when nothing changed. Only states seen by a successful checkpoint can be recovered: edits between checkpoints collapse into one, a file created and deleted between two checkpoints is gone, and the oldest checkpoints are evicted when history exceeds `history.maxBytes` (200 MiB by default; the latest checkpoint is never evicted). Pause editing while a checkpoint runs; a file that changes mid-capture makes it fail with a retry message.

```text
repolore history                                   # checkpoints, their times and the files each holds
repolore restore 2 --path <note> --dry-run         # preview: add, replace or delete
repolore restore 2 --path <note>                   # apply to one path
repolore restore 2                                 # apply a whole checkpoint within its coverage
```

Checkpoint times are ISO 8601 in UTC with six fractional digits, such as `2026-10-24T13:30:00.918757+00:00`.

Applying a restore first checkpoints the current state of everything it will change, and then reports how to undo it: `restore complete: ... (checkpoint 0000000000000003); undo with 'repolore restore 2'` (in JSON, `recoveryId`). `migrate` and `init --update-method` report their undo command the same way. If a restore (or `migrate`, or `init --update-method`) is interrupted, the tool records a pending operation and refuses other writes with the recovery id; run `repolore restore <that id>` to return to the pre-operation state. `history`, `context` and `health-check` keep working meanwhile.

To stop protecting a file, add it to `history.exclude` in `repolore.json` or set `history.enabled` to `false`. That does not erase existing checkpoints, but with history disabled `checkpoint` fails (exit 3), and so does applying a `restore`, `migrate` or `init --update-method`, because each needs a checkpoint of the state before it. `history` and every preview still work. A `history.maxBytes` too small for one checkpoint blocks the same commands.

When history is damaged, `health-check` reports `history-corrupt` and writes are refused rather than risk your notes. The message names the way out: a corrupt stored copy can be deleted, and the next checkpoint of the same content stores a fresh one; a corrupt checkpoint file can be moved out of `_repolore/.history/`, which gives up only that checkpoint. To purge all local history, delete `_repolore/.history/` with ordinary tools when `health-check` shows no pending operation; recovery of everything captured so far is then gone. Checkpoints are local copies on the same disk and do not replace Git or a backup.

## 8. Supported environments, privacy evidence, and verifying a release

| Item | Statement |
|---|---|
| Install | .NET 10 SDK; run on a .NET 10.0.x runtime (`LatestPatch`; a .NET 11 runtime is not used) |
| Operating systems tested | CI on GitHub-hosted `windows-latest`, `ubuntu-latest` and `macos-latest` runners; development on macOS arm64. Other combinations are expected to work and are untested |
| Repository language | Any; the tool does not read source code |
| Network use by the tool | None. Evidence: the build refuses networking, process and dynamic-loading APIs in the three shipped projects; the Linux CI run repeats the install and smoke test with networking blocked, for the NuGet tool and for the Linux x64 executable. There is **no runtime network trace**, so this is evidence, not proof. The native executables link the operating system's networking libraries because the .NET runtime's native layer does; RepoLore's code never calls them. NuGet restore, the npm launcher's Node.js, your package manager, your editor and your agents are separate systems |
| Reporting a vulnerability | Privately, as described in [SECURITY.md](../SECURITY.md) |

To verify a release: compare the SHA-256 of the file you pushed or approved with the checksum file published with the release. Up to 1.0.0 it is `RepoLore.Cli.<version>.sha256`, covering the package and its CycloneDX SBOM `RepoLore.Cli.<version>.cdx.json`. From 1.1.0 it is `repolore-<version>.sha256`, covering every file attached to the release: the archives, the NuGet package and both SBOMs (`repolore-<version>.native.cdx.json` describes the executables; the NuGet package's is `RepoLore.<version>.cdx.json`, `RepoLore.Cli.<version>.cdx.json` up to 1.1.0). A package downloaded from NuGet.org carries NuGet.org's repository signature, so its bytes differ from the pushed file; check it with `dotnet nuget verify <file>`. The package has **no author signature**. The NuGet package's SBOM lists no third-party components because it contains none; the native executables contain the .NET runtime, which their SBOM lists and whose MIT license and notices ship as `THIRD-PARTY-NOTICES.txt` in every archive. Release notes live in `docs/release-notes/`.

## Where to learn more

The design contract and per-area notes are under `_repolore/` in this repository, starting at `_repolore/root.md`.
