# Scenario tests on a reference repository — package 14 design

Status: **14a in progress.** Design agreed with the maintainer on 2026-10-24. The reference repository (39 stages) was published on 2026-10-24 at <https://github.com/R-Elias/repolore-e2e-test-reference> with `main` and all tags pushed. The maintainer activated rulesets: `main` cannot be deleted or force-pushed, and tags cannot be updated or deleted, though new tags can still be created. The runner, pin and workflow do not exist yet. Gates: [implementation guide, section 14](implementation-v1.md#14--scenario-tests-on-a-reference-repository). **14a and 14b must pass before the v1.0.0 cut** (package 13); 14c comes after 1.0.0.

## Purpose

Unit, integration and package tests prove the parts. This suite reproduces what a user does with a real repository over time, with an unmodified published-style package, so that an enterprise adopter meets no surprise. It is also a regression suite: the fixed history plus pinned expectations make a behavior change visible as a diff in a scenario file.

## Principles

- **The reference repository is separate and immutable.** A public GitHub repository, `repolore-e2e-test-reference`, holds a bogus but production-looking monorepo and a deliberately designed history. Tests never write to it; they clone it into a disposable temp directory.
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

## Stages (built 2026-10-24)

The fictional "Harbor" monorepo has 39 commits on `main`, one annotated tag per stage, and one merge. `STAGES.md` in every commit is the authoritative per-tag list: content, intended tool behavior, and the rules (never modify, append-only, tags are the interface, `.gitattributes` `* -text` so every OS checks out identical bytes).

| Tags | Purpose |
|---|---|
| `s01`–`s03` | Plain monorepo; hazard folders (vendored SDK, tracked `tools/bin/`, spaces, accents, 16-level chain, 120 siblings); `.gitignore` ready for `init` |
| `s04`–`s10` | Adoption (real `init` output plus authored path and custom notes), edit, add and remove, one then two sessions (force-added), promotion, cleanup. `s10` is the **baseline** |
| `s11`–`s23` | Each one is the baseline plus one hazard (hazards never accumulate): orphans and a missing method, links and malformed sessions, note content (invalid UTF-8, BOM, CRLF, misnamed note, 1.5 MB note), 1000-directory tree, Unicode paths, policy variants, a one-checkpoint budget, history disabled, invalid ignore rule, invalid history policy, format 2, malformed and duplicate-key markers |
| `s24`–`s27` | Alpha repositories from `s03`: sparse committed, local mirror only, conflicting copies, a loose file in `sessions/` |
| `s28`–`s32` | Back to the baseline; a branch edit and a main edit of one note, merged with a hand resolution; release 1 |
| `s33`–`s36` | Committed `.history/` made by the real tool, then damaged: an altered object, a truncated manifest, an interrupted restore (`pending.json`), and `.history` as a file |
| `s37`–`s39` | OS hazards: a case collision built through the index (Linux only), symlinks, paths over 260 characters |

The repository was built by a one-off script with fixed authors and dates and an isolated Git configuration. Commits through `s32` are byte-reproducible; `s33`–`s35` contain real checkpoint timestamps. Because the repository is append-only, the script is never rerun against it. New stages are appended by hand.

**Local validation (2026-10-24, the `RepoLore.Cli` source build equal to `0.1.0-preview.1`, macOS):** the harness flow (fresh clone, `checkout --force <tag>`, `clean -fd`) was run on all 39 tags. `health-check` matched each stage's intent, and two independent builds gave identical results. Spot checks:

- sessions are isolated, and an invalid session id is a usage error
- `--explain` follows the re-inclusion rule
- the `s17` budget evicts after a checkpoint from another stage
- migration succeeds on `s24` and `s25`, and refuses on `s26` and `s27`
- `s33` and `s34` refuse writes
- `s35`'s pending operation refuses writes, and `restore 2` then recovers to healthy.

Checking out commits cannot reproduce these four IDs: `history-locked`, `operation-interrupted`, `internal-error` and `init-incomplete`. They stay covered by ordinary tests. On macOS, `s37` collapses into one directory, as expected.

**First defect found (not fixed; for a follow-up package):** on `s36`, `checkpoint` and `restore` exit 3 with `io-error` and the raw .NET message "The file '<absolute path>/_repolore/.history' already exists." The message names no remedy and contains an absolute machine path. The read-only `health-check` reports the clear `history-unavailable` finding instead.

## Execution and cost

A dedicated workflow runs the suite on Windows, Linux and macOS **twice a week and on demand** (`schedule` plus `workflow_dispatch` with a tag filter), not on pull requests. The `smoke` tag selects a short subset for on-demand quick runs. Before the 1.0.0 tag the full suite must be run on demand on the exact release commit and be green. A failing scenario uploads its temp directory and command log as an artifact. Scheduled runs only fire from the default branch, and GitHub disables scheduled workflows in a public repository after 60 days without activity.

## Sub-packages

- **14a:** the reference repository (first stages), the pin file, the runner and its self-tests, the workflow, a first smoke set.
- **14b:** the full catalog across the categories above, the ID-coverage test, and the golden outputs for the key commands. **Required before 1.0.0.**
- **14c:** an upgrade matrix (a step option `tool: previous` running an older published package on the same repository). Deferred until a second published version exists; the scenario format already leaves room for it.

## Honest limits

This reduces enterprise surprises; it cannot make them impossible. Corporate proxies, antivirus, OneDrive or network drives, and group-policy locks are not simulated by checking out commits; if they matter, they get a separate documented "hostile environment" category with explicit expected behavior. The suite will find real defects, which are fixed in small follow-up packages, not inside 14.
