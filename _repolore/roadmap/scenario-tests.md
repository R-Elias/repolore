# Scenario tests on a reference repository — package 14 design

Status: **14a in progress.** Design agreed with the maintainer on 2026-10-24. The reference repository (39 stages) was published on 2026-10-24 at <https://github.com/R-Elias/repolore-e2e-test-reference> with `main` and all tags pushed. The maintainer activated rulesets: `main` cannot be deleted or force-pushed, and tags cannot be updated or deleted, though new tags can still be created. The runner, its self-tests, the pin, the workflow and 10 scenarios (9 tagged `smoke`) are implemented and green locally on macOS (see [Implementation](#implementation-14a)). The workflow's first run on GitHub, on all three operating systems, can only happen after the merge to `main`. Gates: [implementation guide, section 14](implementation-v1.md#14--scenario-tests-on-a-reference-repository). **14a and 14b must pass before the v1.0.0 cut** (package 13); 14c comes after 1.0.0.

## Purpose

Unit, integration and package tests prove the parts. This suite reproduces what a user does with a real repository over time, with an unmodified published-style package, so that an enterprise adopter meets no surprise. It is also a regression suite: the fixed history plus pinned expectations make a behavior change visible as a diff in a scenario file.

## Principles

- **The reference repository is separate and immutable.** A public GitHub repository, `repolore-e2e-test-reference`, holds a bogus but production-looking monorepo and a deliberately designed history. Tests never write to it; they clone it into a disposable temp directory.
- **Git history is the script of edits.** The tests may do exactly two things: `checkout <tag>` and `run <repolore command>`. They never create, edit, move or delete files, and never decide what content to write. An edit is a later commit; a deletion is a commit that removes the file; a resolved merge is a merge commit; a corrupt history is a stage that commits broken `.history/` files (force-added), as the frozen `history-failures` fixture already does; sessions are committed, force-added, which also exercises "policies are independent" (the tool reads sessions whether or not Git tracks them). Untracked, Gitignored sessions stay covered by the acceptance journey.
- **One harness rule for `checkout`:** `git checkout --force <tag>` followed by `git clean -fd` (not `-x`), so the working tree equals the stage while the tool's Gitignored `.history/` survives between stages.
- **Append-only.** Stages and tags are never edited or force-pushed. New behavior means a new stage at the end. Tags are protected in the repository settings.
- **Pinned.** This repository holds one small file naming the exact fixture tag or commit (`tests/RepoLore.Cli.Tests/Scenarios/reference-repo.version`). The pin only moves forward. CI clones that version in a build step; the tests only clone that local copy, so the tests need no network. A developer runs the suite with an environment variable pointing at a local clone.
- **Real package, real Git.** The harness runs the installed package (the packed `.nupkg` restored into an isolated cache, as `IsolatedToolEnvironment` does), with its tool manifest in the *parent* of the clone so the clone stays pristine. Git is the real `git`, isolated from the machine's configuration, with `core.autocrlf=false`.
- **Dead simple to maintain.** A scenario is one JSON file. Comments and trailing commas are allowed, and the file is read with `System.Text.Json`, which the CLI tests already use, so no new package is needed. A new case is a new file; a new situation is a new stage plus a version bump of the pin.

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

## Implementation (14a)

On 2026-10-24 the maintainer decided the suite lives in `tests/RepoLore.Cli.Tests/Scenarios/`, not in a new test project. That keeps to "one test project per shipped assembly", and the suite reuses `PackedToolFixture`, `IsolatedToolEnvironment` and the journey's `GitRepository`.

- **Files:**
  - `Scenario.cs`: the model, and a strict parser that rejects unknown keys, a run step without `expect.exit`, a first step that is not a checkout, a name that differs from the file name, and an unknown OS.
  - `ScenarioRunner.cs`: the runner and `TreeSnapshot`.
  - `ScenarioCatalog.cs`: case discovery, OS and tag selection, and `ReferencePin`.
  - `ScenarioFixture.cs`: the collection fixture and `[ScenarioTheory]`.
  - `ScenarioTests.cs`: one theory row per case.
  - `ScenarioRunnerTests.cs`: the self-tests.
  - `Cases/*.jsonc`: the scenarios.
  - `reference-repo.version`: the pin, with `repository`, `tag` and `commit` lines.
- **Run:**
  - The fixture packs the shipped projects once and restores the exact package into an isolated cache.
  - Each scenario gets a work directory holding the tool manifest. Below it, `repo/` is a `--no-checkout` clone of the local reference clone, so the clone stays exactly as the reference repository has it.
  - The fixture refuses to run unless the reference clone resolves the pinned tag to the pinned commit.
  - Read-only commands get the automatic before-and-after snapshot check. These are `context`, `path`, `tree`, `history`, `health-check`, `version`, any `--dry-run` or `--help`, and `migrate --check`. The snapshot covers every path, plus each file's modification time and hash, including `.git` and `.history`, and records symbolic links without following them. Directory times are left out, and each file's time is re-read from the file itself. On Windows, directory enumeration returns the directory entry's copy of a file's timestamps, which NTFS updates lazily. That made the first Windows CI run of the PR fail the checkout self-test with no write at all (2026-10-24); macOS and Linux passed. A failure lists the differing snapshot lines.
  - The first failing step ends the scenario. Golden output files are deferred to 14b.
- **Variables:**
  - `REPOLORE_REFERENCE_REPO`: the local clone. Without it, the scenarios are reported as skipped.
  - `REPOLORE_SCENARIO_TAGS`: optional comma-separated tags.
  - `REPOLORE_SCENARIO_ARTIFACTS`: where a failed scenario's `log.txt` and files, without `.git`, are copied.
- **Locally:** `REPOLORE_REFERENCE_REPO=<clone> dotnet test -c Release tests/RepoLore.Cli.Tests --filter "Category=Scenario"`.
- **CI:**
  - `.github/workflows/scenarios.yml` runs Monday and Thursday at 04:17 UTC, plus `workflow_dispatch` with a `tags` input, on all three OSes. It clones the pinned repository in a build step (with `core.longpaths`) and verifies the pin. A failure uploads `scenario-failures-<os>`.
  - `ci.yml` and `publish.yml` exclude `Category=Scenario` from the ordinary Cli step, so pull requests do not run scenarios. The runner self-tests have no `Category` and still run on every pull request.
- **Scenarios:**
  - `adopt-edit-and-restore-a-note`
  - `sessions-are-explicit-and-isolated`
  - `orphan-and-missing-notes-are-reported`
  - `alpha-knowledge-migrates-and-rolls-back`
  - `alpha-conflicts-block-migration`
  - `corrupt-history-blocks-writes`
  - `truncated-manifest-blocks-history` (not smoke)
  - `interrupted-restore-recovers`
  - `unknown-and-broken-formats-are-refused`
  - `repository-without-knowledge`

  Every expectation was checked against a manual run of the source build on that stage before it was written.
- **Evidence (2026-10-24, macOS arm64):**
  - The runner self-tests pass, 33 of 33. They cover every expectation kind failing with its step named, JSON comparison by value, the read-only check catching a write for four read-only command forms and catching a rewrite with identical bytes, writes being allowed for write commands, and checkout resetting tracked files, removing untracked ones and keeping the ignored `.history/`. They also cover the first failure ending the scenario, an unknown stage failing with a clear message, the reference repository never being written, parser rejections, OS and tag selection, a catalog check, and a pin check.
  - Three deliberate mutations were each caught: disabling the read-only check, dropping `git clean`, and ignoring the exit code.
  - The scenarios pass twice, 10 of 10, against the local clone of the published repository, in about 10 to 14 seconds. A deliberately wrong expectation in the built case file failed with `step 5 (repolore checkpoint): stdout: missing …` and left `log.txt` plus the clone's files in the artifacts directory. `REPOLORE_SCENARIO_TAGS=recovery` selected two scenarios. Without `REPOLORE_REFERENCE_REPO`, the scenarios are skipped and the fixture is not built.
  - With the CI filters, all suites pass: Core 101, Infrastructure 80, Cli 136 excluding Packaging and Scenario (which includes the self-tests), and Packaging 18. The reference clone stayed clean.
- **Not yet done:** the GitHub run on Windows, Linux and macOS. It was possible from the merge of PR #5 on 2026-10-24.

## Catalog (14b, branch `package-14b/scenario-catalog`)

There are 28 scenarios. One is Linux only (`case-collision`) and one is Linux and macOS only (`symlinks-are-not-followed`); the other 26 run on every OS. They are grouped by tag:

- `smoke`: the nine 14a smoke scenarios, plus `init-reproduces-the-adoption`. On `s03`, `init` must produce exactly the marker and method committed at `s04`.
- **Health and sessions:**
  - `link-and-session-hazards`: broken and escaping links, a durable link into a session, a session without `root.md`, and an invalid session id, which is a usage error. A session's link into another session is not checked.
  - `note-content-hazards`: invalid UTF-8 and a misnamed note are findings. A BOM and CRLF are read normally. The 1.5 MB note is omitted from context for budget, with exit 0.
- **Configuration:**
  - `invalid-policies-are-reported`
  - `history-disabled-refuses-history-writes`
  - `small-budget-evicts-the-oldest-checkpoint`
  - `policy-variants`: re-inclusion in `--explain`, and `history.exclude` keeping a note out of checkpoints.
- **Recovery and Git:**
  - `restore-refusals`: unknown checkpoint, a path outside coverage, and a no-op.
  - `merged-note-edits-are-recoverable`
  - `added-and-removed-notes-are-recoverable`
  - `session-lifecycle`: a deleted session is restored from history.
- **History:** `history-path-is-a-file`.
- **Migration:** `alpha-init-refusal-and-local-tree-migration`.
- **CLI:** `usage-errors`. `repository-without-knowledge` also checks `version` and `--help`.
- **Hazards:**
  - `large-and-unusual-paths`: hazard folders, 1000 directories, non-ASCII names, paths over 260 characters.
  - `case-collision` (Linux only).
  - `symlinks-are-not-followed` (Linux and macOS): links are neither captured nor followed.
- **Golden:** `key-outputs-match-golden-files`, plus the `history` step of `session-lifecycle`.

Every command now has a normal case and a failure case. `path` and `tree` refuse format 2, and `version` and `--help` work outside a knowledge base.

**Golden files** (`Cases/golden/*.txt`, six files, reviewed against the stage content on 2026-10-24):
- The `golden` expectation compares the whole stdout after normalizing line endings and masking ISO timestamps as `<timestamp>` and the clone's absolute path as `<repo>`.
- `REPOLORE_SCENARIO_WRITE_GOLDEN=1` writes missing or different golden files into the source tree for review. That step still fails, so a run that writes golden files never passes.
- `tree` order is pinned as implemented: per directory, its subdirectories, then its files, sorted ordinally, then each subdirectory's contents.

**ID coverage** (`ScenarioCoverageTests`):
- Every ID in `expected-ids.tsv` must be asserted by a scenario, either as a JSON `...id` value or as a finding line `<id> _repolore...`.
- The alternative is a reviewed exemption that names the ordinary test asserting it. These exemptions are listed below.
- An exemption fails as soon as a scenario covers its ID, and `internal-error` is the only one allowed without a test.
- A mutation check passed: removing `history-path-is-a-file` makes the test name `history-unavailable` and `io-error`.

| ID | Why no scenario | Asserted by |
|---|---|---|
| `history-locked` | Needs a second writer holding the lock | New `FailureIdTests.Checkpoint_while_another_writer_holds_the_lock_fails_with_history_locked` (CLI process) |
| `operation-interrupted` | Needs a write to fail mid-restore | New `FailureIdTests.A_restore_that_cannot_write_a_file_is_interrupted_and_names_its_recovery_id`: the note's directory is read-only on Unix, and the note is held open with read-only sharing on Windows. It checks `recoveryId` 2, then `restore 2` recovers |
| `init-incomplete` | Only fault injection fails the first checkpoint after init created files. A history path that is a file or read-only fails earlier, with `io-error` | `InitEngineTests.Failure_creating_the_first_checkpoint_is_visible_and_retryable` (engine level) |
| `migration-blocked` | `migrate` prints a blocked plan before the engine runs; the engine refusal behind it is defensive | `MigrationEngineTests.Differing_copies_conflict_and_write_nothing` (engine level) |
| `internal-error` | Only raised by a defect | none |

Until 14b, no CLI-level test asserted `history-locked` or `operation-interrupted`. They appeared only in `expected-ids.tsv`.

**Evidence (2026-10-24, macOS arm64):**
- The scenarios pass twice, 27 of 27 (28 minus the Linux-only one), in about 26 seconds, and the reference clone stayed clean.
- With the CI filters: Core 101, Infrastructure 80, Cli 146 excluding Packaging and Scenario (which includes 38 scenario self- and coverage tests and the 2 `FailureIdTests`), and Packaging 18.
- Not run yet: Windows, Linux (`case-collision` has never run anywhere), and the scheduled workflow with this catalog.

**Open contract question (for the maintainer):** on `s19`, an invalid `_repoloreignore` rule is a blocking `policy-error` in `health-check` (exit 3). Yet `checkpoint` succeeds and `context` reads normally. Package 03 says "reject … invalid rules before mutations", while ignore rules affect only source discovery, never history coverage. Both readings are defensible, so the scenario asserts only the `health-check` finding until this is decided.

**Defect still open:** the `s36` `io-error` message is raw .NET text containing an absolute path. Only its ID is pinned.

## Sub-packages

- **14a:** the reference repository (first stages), the pin file, the runner and its self-tests, the workflow, a first smoke set.
- **14b:** the full catalog across the categories above, the ID-coverage test, and the golden outputs for the key commands. **Required before 1.0.0.**
- **14c:** an upgrade matrix (a step option `tool: previous` running an older published package on the same repository). Deferred until a second published version exists; the scenario format already leaves room for it.

## Honest limits

This reduces enterprise surprises; it cannot make them impossible. Corporate proxies, antivirus, OneDrive or network drives, and group-policy locks are not simulated by checking out commits; if they matter, they get a separate documented "hostile environment" category with explicit expected behavior. The suite will find real defects, which are fixed in small follow-up packages, not inside 14.
