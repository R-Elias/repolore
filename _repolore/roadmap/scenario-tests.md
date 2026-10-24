# Scenario tests on a reference repository — package 14 design

Status: **designed, not started** (design agreed with the maintainer on 2026-10-24). Gates: [implementation guide, section 14](implementation-v1.md#14--scenario-tests-on-a-reference-repository). **14a and 14b must pass before the v1.0.0 cut** (package 13); 14c comes after 1.0.0.

## Purpose

Unit, integration and package tests prove the parts. This suite reproduces what a user does with a real repository over time, with an unmodified published-style package, so that an enterprise adopter meets no surprise. It is also a regression suite: the fixed history plus pinned expectations make a behavior change visible as a diff in a scenario file.

## Principles

- **The reference repository is separate and immutable.** A public GitHub repository (placeholder name `repolore-reference-repo`) holds a bogus but production-looking monorepo and a deliberately designed history. Tests never write to it; they clone it into a disposable temp directory.
- **Git history is the script of edits.** The tests may do exactly two things: `checkout <tag>` and `run <repolore command>`. They never create, edit, move or delete files, and never decide what content to write. An edit is a later commit; a deletion is a commit that removes the file; a resolved merge is a merge commit; a corrupt history is a stage that commits broken `.history/` files (force-added), as the frozen `history-failures` fixture already does; sessions are committed, force-added, which also exercises "policies are independent" (the tool reads sessions whether or not Git tracks them). Untracked, Gitignored sessions stay covered by the acceptance journey.
- **One harness rule for `checkout`:** `git checkout --force <tag>` followed by `git clean -fd` (not `-x`), so the working tree equals the stage while the tool's Gitignored `.history/` survives between stages.
- **Append-only.** Stages and tags are never edited or force-pushed. New behavior means a new stage at the end. Tags are protected in the repository settings.
- **Pinned.** This repository holds one small file naming the exact fixture tag or commit (`tests/RepoLore.Scenarios.Tests/reference-repo.version`). The pin only moves forward. CI clones that version in a build step; the tests only clone that local copy, so the tests need no network. A developer runs the suite with an environment variable pointing at a local clone.
- **Real package, real Git.** The harness runs the installed package (the packed `.nupkg` restored into an isolated cache, as `IsolatedToolEnvironment` does), with its tool manifest in the *parent* of the clone so the clone stays pristine. Git is the real `git`, isolated from the machine's configuration, with `core.autocrlf=false`.
- **Dead simple to maintain.** A scenario is one JSON file (comments allowed, read with Newtonsoft). A new case is a new file; a new situation is a new stage plus a version bump of the pin.

## Scenario format (about eight expectation kinds)

```jsonc
{
  "name": "restore-undoes-a-note-edit",
  "tags": ["smoke", "recovery"],
  "os": ["windows", "linux", "macos"],
  "steps": [
    { "checkout": "s04-knowledge-v1" },
    { "run": "checkpoint", "expect": { "exit": 0, "stdout": ["checkpoint 0000000000000001"] } },
    { "checkout": "s05-note-edited" },
    { "run": "checkpoint", "expect": { "exit": 0, "stdout": ["1 changed"] } },
    { "run": "restore 1 --path _repolore/sparse-tree/services/orders/orders.md",
      "expect": { "exit": 0, "file-equals": { "path": "_repolore/sparse-tree/services/orders/orders.md", "at": "s04-knowledge-v1" } } }
  ]
}
```

Expectations: `exit`; `stdout`/`stderr` containing or not containing text; `json` field values; `file-equals` (the file equals its content at another stage, so Git, not the tool, is the oracle); `file-absent`; `golden` (full normalized output compared with a reviewed file, timestamps masked). Read-only commands (`context`, `path`, `tree`, `history`, `health-check`, every `--dry-run`) get an automatic check that the working tree and `.history/` are byte-identical before and after. A coverage test requires every finding and error ID in the frozen `expected-ids.tsv` to be asserted by at least one scenario.

The vocabulary stays this small on purpose; a need for more is a signal that the case belongs in an ordinary test.

## Initial stage plan (subject to the maintainer's review)

| Stages | Purpose |
|---|---|
| `s01`–`s03` | Plain monorepo with no RepoLore: services, web, libraries, infrastructure, docs; hazard folders (`vendor`, `bin`, `obj`, spaces, accents, very deep and very wide); then a `.gitignore` ready for `init`. The adopter's starting point |
| `s04`–`s06` | Knowledge adopted (marker, method, root, path notes, custom `architecture/` and `adr/`), a note edited, notes added and removed. Checkpoint, restore and history flows |
| `s07`–`s10` | One session, two sessions, a promotion, a cleanup |
| `s11`–`s13` | Directories moved or deleted (orphan notes); broken and escaping links, a link into a session, invalid UTF-8, BOM, CRLF, a name mismatch, an oversized note |
| `s14`–`s16` | A huge tree, Unicode paths, configuration variants (history exclusions, a tiny budget, ignore rules with re-inclusion, an invalid rule) |
| `s17` | Unsupported format, malformed JSON, duplicate keys |
| `s18`–`s21` | Realistic alpha repositories for `migrate`: sparse-only, local-only, conflicting copies, a `sessions/` naming conflict |
| `s22`–`s23` | A resolved merge conflict in a note; a "release 1" baseline |
| `s24`–`s26` | Corrupt history, a pending operation, and operating-system hazards (case collisions, symlinks, long paths), each scenario tagged with its `os` list |

A `STAGES.md` in the reference repository states each stage's purpose and the scenarios that rely on it.

## Execution and cost

A dedicated workflow runs the suite on Windows, Linux and macOS **twice a week and on demand** (`schedule` plus `workflow_dispatch` with a tag filter), not on pull requests. The `smoke` tag selects a short subset for on-demand quick runs. Before the 1.0.0 tag the full suite must be run on demand on the exact release commit and be green. A failing scenario uploads its temp directory and command log as an artifact. Scheduled runs only fire from the default branch, and GitHub disables scheduled workflows in a public repository after 60 days without activity.

## Sub-packages

- **14a:** the reference repository (first stages), the pin file, the runner and its self-tests, the workflow, a first smoke set.
- **14b:** the full catalog across the categories above, the ID-coverage test, and the golden outputs for the key commands. **Required before 1.0.0.**
- **14c:** an upgrade matrix (a step option `tool: previous` running an older published package on the same repository). Deferred until a second published version exists; the scenario format already leaves room for it.

## Honest limits

This reduces enterprise surprises; it cannot make them impossible. Corporate proxies, antivirus, OneDrive or network drives, and group-policy locks are not simulated by checking out commits; if they matter, they get a separate documented "hostile environment" category with explicit expected behavior. The suite will find real defects, which are fixed in small follow-up packages, not inside 14.
