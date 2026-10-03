# First Release — Implementation Work Packages

Status: **packages 01–08 implemented (01–02 CI-green; 03–08 local evidence pending CI)**. Target: a preview followed by v1.0.0 of the single `RepoLore.Cli` NuGet tool. This guide decomposes the [roadmap](roadmap.md); [invariants](../product/invariants.md) and the [session contract](../product/sessions.md) still apply. It resolves earlier open implementation choices below. Do not copy alpha mirror-generation behavior into v1.

## How to execute this plan

Complete one work package at a time, in dependency order. Each should produce a reviewable change with its own passing gate. A compiling stub, happy-path demonstration, or updated checkbox is not completion. Do not start destructive commands until the preservation gates pass. Preserve unrelated working changes.

Use small test projects (one per shipped assembly) with fixture files and a narrow fault-injecting filesystem wrapper. No daemon, plugin, benchmark, or research infrastructure. The shipped projects stay BCL-only, but test projects may reference the approved test stack (xUnit, FluentAssertions, FsCheck, CliWrap, Newtonsoft.Json) — that is a test-only exception and never weakens the shipped binary's boundary. Tests may invoke the CLI as a process; the shipped CLI must not spawn processes. Inject time and IDs rather than asserting wall-clock timestamps or sleeping in tests.

For every work package, leave: the implemented behavior, exact check command and result, relevant fixture names, and any remaining limitation. Update the affected RepoLore note when behavior changes. A failure gate means fix the defect before claiming completion; it does not mean ask the user about a routine implementation choice. If the contract itself cannot be met, document the precise conflict before changing scope.

| Order | Work package | Depends on | Exit evidence |
|---|---|---|---|
| 01 | Executable boundary and fixtures | — | Guards fail on forbidden capability; real version command works |
| 02 | Filesystem paths and knowledge mapping | 01 | Mapping is reversible and filesystem escapes are refused |
| 03 | Configuration and coverage policies | 02 | Exact policy decisions, independent of Git and context selection |
| 04 | Durable and session context | 02–03 | Deterministic selected content; reads write nothing |
| 05 | Snapshot capture and publication | 02–03 | Old and new bytes recoverable; interrupted capture is invisible |
| 06 | Retention and mutation ownership | 05 | Bounded retained state without broken checkpoints |
| 07 | Restore and interrupted-operation recovery | 04–06 | Restore, undo, and failure recovery all preserve scoped bytes |
| 08 | Initialization and method updates | 07 | Repeated init is harmless; first baseline exists |
| 09 | Alpha migration and rollback | 07–08 | Both alpha variants preserved; complete semantic rollback |
| 10 | Health checks and final CLI contract | 03–09 | Stable findings and exit codes, no repair side effects |
| 11 | Packaged tool and restricted installation | 10 | Actual .nupkg works with clean caches and local-only feeds |
| 12 | Dogfood and NuGet preview | 11 | Full human/agent workflow, including session promotion |
| 13 | Signed v1.0.0 release | 12 | Verified release artifact and versioned recovery documentation |

Work on 04 and 05 can proceed independently once paths and coverage are stable. Do not split snapshot, retention, and restore across incompatible implementations. Release credentials can be arranged while implementation proceeds; never bypass the signing gate to label a preview enterprise-ready.

## 01 — Establish the executable boundary, not a framework

**Deliver:** `Core`, `Infrastructure`, `Cli`, and three test projects (one per assembly). Only Cli is packable. Set the .NET 10 target, pin SDK/package versions, and expose a real `version` command carrying CLI version and supported knowledge formats. Keep format version independent of package version.

Build-time guards cover all shipped projects: no networking APIs, process execution, dynamic loading/native interop used to evade guards, or third-party runtime dependencies. Core plans operations through narrow interfaces; Infrastructure owns physical IO. Add abstractions only as the following packages need them. Read console arguments/environment at the boundary and pass values explicitly.

Create small named fixtures reused below: `minimal-v1`, `two-sessions`, `directory-mapping`, `alpha-sparse-only`, `alpha-local-only`, `alpha-conflict`, and `history-failures`. Put distinctive sentinel text in unrelated files so accidental reads/writes are observable. Author expected results independently of the implementation under test.

**Pass:** builds/tests on Windows, Linux, macOS; `version` needs no repository and writes nothing. A temporary forbidden API reference makes the guard fail, then removing it restores green. Inspect the produced runtime dependency list, not only project declarations.

**Fail:** empty handlers return success; tests require network access *at runtime* (restoring test-framework packages from the pinned feed is build tooling, not a runtime test dependency); public Core package or plugin interfaces appear; CI only tests one OS while claiming all three. No requirement for a separate Guard/Perf/Compat project.

## 02 — Freeze safe paths and a one-to-one directory mapping

**Deliver:** one shared path resolver for reads, writes, manifests, and migration. CLI paths are repo-root-relative; absolute paths may be accepted only if canonicalized inside the root. Knowledge flags use `_repolore/...`. Reject `..` escapes, rooted manifest paths, drive/UNC substitutions, and symlink/reparse traversal, including an existing ancestor of a nonexistent destination. A string prefix check is insufficient (`repo-other` is not inside `repo`).

Do not infer case behavior solely from OS name. Detect actual directory/file aliases when planning writes; refuse ambiguous case/Unicode aliases on the destination filesystem. Preserve source spelling. Report unsupported filenames/lengths before writing rather than silently truncating or normalizing names. A checkpoint and restore preserve bytes; text rendering may tolerate UTF-8 BOM/CRLF without rewriting them.

**Mapping decision:** only directories are nodes. Each directory maps to one note named after it, stored inside the mirrored directory; files never get notes, so no escaping is required. The repository root maps to the fixed `root.md`. Mapping depends on the path string, never on which siblings exist. Decode accepts only canonical directory-note paths; anything else (a file-like note, a note name that does not match its directory, or a top-level note other than `root.md`) produces a finding, not a guessed path. Keep mapping knowledge in one component.

Examples:

| Source target | Path under `sparse-tree/` |
|---|---|
| `src/` | `src/src.md` |
| `src/sub/` | `src/sub/sub.md` |
| root | `root.md` |

**Pass:** table-driven encode/decode round trips, nested directories, the root `root.md` case, and real filesystem alias/escape checks. Adding/removing a sibling never relocates another note. Files inside a directory produce no notes. Sentinels outside the root remain unread/unmodified after malicious target/manifest inputs.

**Fail:** assigning a note to an individual file; any escaping/hex scheme; universal case-folding that merges two Linux files; only the happy path tested. Over-long names: refuse affected writes with a path-specific error, do not silently truncate.

## 03 — Make policy decisions explainable and independent

**Deliver:** format/config parser and one small matcher. Missing marker is alpha; malformed JSON is an error, never an alpha fallback. Missing optional history values use documented defaults. Reject duplicate known JSON keys, wrong types, negative/nonintegral/overflowing budgets, and invalid rules before mutations. Preserve unknown fields on explicit rewrites. Ordinary reads do not rewrite configuration.

**Matcher decision:** paths use `/` and ordinal case-sensitive matching. Every rule is root-relative; optional leading `/` is cosmetic. A bare `build` only matches root `build`; use `**/build/` for any depth. `*` and `?` do not cross `/`; `**` must be a whole segment and matches zero or more segments. A trailing `/` matches a directory and its descendants, not a same-named regular file. An excluded directory's exclusion applies to descendants unless a later rule re-includes them. Last matching rule wins; leading `!` includes. Blank lines and lines starting `#` are ignored; `\#`/`\!` at the start mean literal leading characters. Do not trim meaningful spaces, add shell expansion, or silently claim other Git syntax works. Reject unsupported escape/glob syntax with line numbers.

Source discovery applies: hard safety exclusions → configurable generated-directory defaults → user `_repoloreignore` overrides. Default rules use `**/bin/`, `**/obj/`, `**/node_modules/`, etc.; document the exact finite list. No Git invocation. For v1, if any include/negation rule exists, simply traverse user-excluded directories while still excluding their contents unless re-included. Continue pruning hard exclusions. Do not build a clever possible-descendant analyzer.

History coverage is a separate rule evaluation over eligible regular files: top-level controls plus all authored Markdown, including all sessions. Its exclusion paths are repo-root-relative with the same matcher grammar. Always exclude `.history/`, tool temporary paths, and symlinks; ordinary source-discovery rules never apply to authored history. Selecting one session for context never limits checkpoint coverage. Compare recorded coverage as well as content when deciding whether a checkpoint is unchanged.

**Pass:** fixed rule/output table includes `build/a`, `src/build/a`, re-inclusion through an excluded parent, a real authored `vendor/` override, invalid `ab**cd`, rule ordering, and hard exclusions. A Gitignored session is captured; adding it to `history.exclude` changes only future history coverage. No policy change deletes a file.

**Fail:** silently skipping malformed rules in a mutating command; reporting Git tracking status without consulting Git; filtering recovery using the source walker. `health-check --explain` reports RepoLore decisions and matching rules, and explicitly leaves actual Git status to Git.

## 04 — Deliver usable context before enabling mutations

**Deliver:** `path`, `context`, and `tree`, with alpha read support and explicit session scope. Logical target absence is allowed; explicit requested note/session absence is a finding. Required root/method absence is also a finding when requested. Alpha dual copies with different non-empty bytes are conflicts, never a silent preference.

**Selection decision:** order is requested method → durable root/ancestors/target when a source target is present → selected session root → repeated `--node` arguments in argument order. Deduplicate by canonical physical path, retaining first position. `--node` alone reads only explicit nodes (plus method if requested). No selector is a usage error. Invalid `--session` is an error even if the budget would omit it. Labels identify durable versus provisional session sources.

Session IDs are one portable directory component: ASCII letters, digits, `_`, `-`, up to 100 characters, with an alphanumeric first character. Reject Windows reserved device names (`CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`, `LPT1`–`LPT9`) case-insensitively. No implicit current/latest session. Session notes require matching `--session`; reject nodes in another session or history. Listing `_repolore/sessions/` is explicit and lists IDs; default `tree` does not enumerate its contents. Links are reported, never auto-read or fetched.

**Budget decision:** render each candidate into one canonical block: `---\n## {repoRelativePath} [{scope}]\n\n{text}\n`, where scope is `durable` or `session:<id>`, paths use `/`, and `text` is UTF-8-decoded content with a leading BOM removed, CRLF normalized to LF, and outer whitespace trimmed. Invalid UTF-8 is a finding, not replacement text. Charge the sum of `ceil(block.Length / 4)` per included block, with .NET `String.Length` (UTF-16 code units), identically for JSON and text selection. Never call it an actual model-token limit. Select in order, skip a non-fitting note, and continue to smaller notes. Report omitted source/reason metadata separately; metadata itself is not promised to fit the content allowance. Validate all selectors before rendering so invalid or cross-session selections do not return a partial successful-looking payload. A positive budget is required; zero/negative is usage error. `--strict` exits 1 on budget omissions while still returning the partial result. Plain and JSON render the same logical included/omitted sets.

**Pass:** the two-session fixture has unique sentinels in both sessions. Default durable context contains neither; selecting A includes only A's root and explicitly named nodes. An oversized ancestor cannot hide a later fitting target note. Duplicate nodes appear once. All reads leave the contents, existence, and modification times of repository/history files unchanged (ignore filesystem access times).

**Fail:** assembling all Markdown and trimming afterward; silently following links; opening session B to decide it is irrelevant; session content presented as authoritative policy; budget omissions returning strict success.

## 05 — Capture complete versions safely

**Deliver:** `checkpoint` and `history` backed by plain files, plus the basic exclusive writer guard used for manifest IDs/publication. Package 06 exercises that same guard across processes and adds retention; do not leave checkpoint callable without it. Recommended fixed layout: `.history/objects/<sha256>`, `.history/checkpoints/<monotonic-id>.json`, `.history/tmp/`, `.history/write.lock`, and later `.history/pending.json`. History has its own `historyVersion`; do not confuse it with knowledge format or CLI SemVer. Ignore incomplete files during read-only enumeration; never silently accept a malformed completed manifest.

Each manifest records ID, timestamp, history/knowledge versions, capture scope and exclusion rules, and sorted repo-relative file paths with byte hashes and sizes. Scope records eligible absence: an absent `repolore.json` is different from an uncaptured/excluded one. Session scope covers eligible Markdown in all session folders. Alpha checkpointing preserves both tree and sparse-tree physical paths without interpreting or merging conflicting notes, so users can protect variants before resolving them.

Stream original bytes to a temporary object while hashing; validate existing objects before reuse. Publish immutable objects before publishing the completed manifest with a same-filesystem rename. Publish the manifest last. Do not update a mutable “latest” pointer: highest valid completed ID determines order. IDs increase under the write guard, independently of clock changes.

Enumerate candidates and detect observed changes during capture through a second inventory/hash verification. If detected, fail without publishing a checkpoint. This does not promise an atomic view while arbitrary editors write; instruct users to pause edits. No-op compares paths, hashes, and effective history scope/policy including budget, not timestamps alone. Explicit checkpoint with disabled history exits 3 with a policy explanation; retained history remains readable. Never restore from trimmed or newline-normalized rendering text.

**Pass:** fixture A → edited A plus new B → deleted A yields exactly three distinguishable manifests and recoverable original bytes. Changing only mtime is no-op; changing bytes with the same length/mtime is captured. Distinct paths with identical bytes share an object. Scope/exclusion changes are recorded even if visible hashes match. Alpha conflicts are captured as separate paths.

**Fail:** replacing an object at a known hash; exposing a manifest before its objects; treating a missing file as empty content. Inject failures after object write and before manifest publication: the previous completed checkpoint remains valid, authored files are untouched, and `history` does not list the failed attempt.

## 06 — Bound history without invalidating recovery

**Deliver:** retention and cross-process/crash validation of the shared writer guard from 05 before any restore/update/migration handler is enabled. Use a persistent lock file with exclusive open/OS locking; process death releases ownership. Do not infer ownership solely from file existence or delete another process's lock by age. A second writer exits 3 with a retry message. Read-only commands do not create locks.

Count exact retained object and manifest byte sizes against `history.maxBytes` (default 209715200). Temporary staging and the tiny pending/lock controls are extra and documented. Count shared objects once. Compute the proposed retained set first, publish the new valid checkpoint, then evict oldest unprotected manifests and finally unreferenced objects. Never delete an object still needed by a retained or pending transaction. If cleanup fails, preserve validity and report that the budget was not achieved; retry cleanup during a later mutation, never a read.

Normal capture must fit its latest snapshot. A destructive operation must preflight that both its complete pre-operation checkpoint and planned resulting checkpoint fit together, counting shared objects once. Pin the selected restore target during the operation as well; preflight the entire retained protected set, including that target if its objects are not shared by the required pair. If necessary retained state does not fit, abort before authored-file changes; no implicit budget increase or coverage reduction. Successful undo availability lasts until later explicit history activity legitimately evicts that checkpoint, not merely until the same restore finishes.

**Pass:** byte-sized fixtures demonstrate oldest-first eviction, shared-object retention, changed policy, exact-at-budget success, one-byte-over failure, and a new state that fits alone but cannot retain the undo pair. An over-budget failure leaves the previous completed checkpoint and authored files usable. A killed lock owner does not permanently block later writes.

**Fail:** evicting the pre-restore checkpoint while finishing restore; deleting old valid history before replacement publication; promising the budget includes transient disk usage; claiming cleanup success after deletion errors.

## 07 — Make restore reversible, including interruption

**Deliver:** a pure plan first, then guarded application. `restore <id> --path <path>` selects one exact eligible file path, including an absent path within saved coverage, not an implicit directory glob; full restore handles a whole snapshot. Unknown IDs or corrupt target objects fail before any authored mutation. Preview lists additions, replacements, deletions, and unchanged files with reasons; it writes nothing and does not promise later application against changed input. Recompute/revalidate the plan at apply time. If the complete plan is unchanged, report no-op without creating a pre-operation checkpoint or pending transaction.

**Scope decision:** ordinary restore changes only files eligible under both the saved coverage and the current coverage. Build the candidate union of saved paths and currently existing eligible paths. Absence in a snapshot permits deletion only when that path was within saved coverage; never infer deletion for a previously excluded/newly covered path. If an explicit `--path` is excluded by either side, fail with a scope explanation. Unrelated source files and all history internals are outside ordinary restore scope.

For controls such as configuration, freeze pre-operation policy for the whole transaction. Restoring disabled history must not disable the transaction's post-checkpoint. Validate all destinations, actual aliases, hashes, types, and budget before writing; recheck before each replacement. Require edits to pause. Refuse hardlinked affected files or break links safely through replacement; never truncate a hardlink and thereby modify an outside file.

**Transaction decision:** write a small durable `pending.json` before the first authored mutation. It records a validated affected-path plan with before/after hashes or absence, target ID, protected pre-operation ID, historyVersion, and frozen pre-operation configuration/coverage/budget. Recovery validates this independent history record instead of relying on a possibly damaged current knowledge marker/configuration. Stage writes in the destination filesystem; replace individual files safely. Whole-directory atomicity is not claimed. Write the resulting checkpoint before clearing pending state.

After an interrupted mutation, normal mutators refuse with the recovery ID. `history` and safe reads remain available. `restore <pre-operation-id>` is the explicit recovery route: restore exactly the affected before-images/absence from the pending plan, validate current files against recorded before/after states, and refuse unexpected new edits rather than discarding them. It is retryable and does not start a nested recovery transaction. Publish or confirm a completed checkpoint of the verified recovered before-state under the frozen policy before clearing pending state. The pending plan pins recovery objects against all retention until resolved. No hidden replay on ordinary reads.

**Pass:** create/modify/delete fixture restores to an old snapshot, and restoring its recorded pre-operation ID undoes that restore byte-for-byte. An excluded file remains untouched. Introduce failure before the first replacement, between two replacements, and after writes but before final checkpoint: each reports failure and the same usable recovery ID. Restart and recover the exact before-state. A fresh unrelated edit during recovery is detected, not overwritten.

**Fail:** reporting success with a partial restore; removing current exclusions to make restore convenient; repairing a corrupt object from an arbitrary current file; erasing pending metadata on startup; recovery requiring a valid current knowledge marker that the interrupted migration itself damaged.

## 08 — Initialize minimally and update methods explicitly

**Deliver:** `init` creates only missing entry points/configuration and the canonical tree, followed by a completed first checkpoint when history is enabled. It does not inspect source to invent knowledge, create empty nodes, start a session, regenerate sparse-tree, or rewrite `.gitignore`/agent instructions. Report required Git exclusions for sessions/history and how to apply them.

An existing nonempty `_repolore/` without marker is alpha, not a v1 install to overwrite. An absent, empty, or tool-temporary-only directory can be initialized: atomically publish a valid configuration/marker before creating authored templates. Failure before that publication leaves no authored files; failure afterward has a valid v1 marker so retry can safely create only missing files. Invalid marker/configuration fails; explicit migration handles legacy. `init --update-method` uses package-embedded v1 guidance and the same pre/post preservation transaction as restore. Unchanged embedded bytes are no-op. Existing root/custom/session notes are never replaced by templates.

If first capture fails, report initialization incomplete with the paths already created; do not erase them or claim recovery exists. Re-running must complete missing initialization safely. Respect an explicit valid `history.enabled=false` setting and state that protection is disabled; destructive updates still refuse without preservation.

**Pass:** empty directory initializes with one baseline and no source placeholders; second init changes nothing. Preexisting root/config/custom notes survive exactly. Failure creating the first checkpoint is visible and retryable. Method update can be undone to exact old bytes.

**Fail:** nonempty alpha sparse-only clone becomes empty; user-edited method is replaced during ordinary init; failed init rolls back by deleting a directory containing user work.

## 09 — Migrate alpha without choosing the user's knowledge

**Deliver:** explicit `migrate --check`, `--dry-run`, and apply. Inventory both alpha trees, custom/session areas, and marker absence. Preflight conflicts and destinations first; conflicts and dry-runs write nothing. On a clean apply, checkpoint physical variants before mutation. A separate explicit alpha checkpoint can protect variants before a human resolves conflicts. Read-only legacy handling can remain useful while migration is blocked.

Classify each mapping: only one useful variant → candidate; identical variants → candidate; differing useful variants → conflict; ambiguous old naming → conflict even if current source existence suggests an answer. Do not overwrite a reserved `sessions/` custom area merely because v1 reserves the name: if existing content does not fit session layout, report a naming conflict requiring deliberate relocation. Preserve all variants before manual resolution with alpha `checkpoint`.

Use the mapping from 02 for destinations. Stage the full destination plan and verify hashes before mutation. Remove only verified redundant legacy files after preservation; if legacy directories contain unknown/non-Markdown content, leave that content and report it rather than recursively deleting. Marker changes last, but do not mistake marker-last for crash atomicity. The pending transaction covers both layouts and all explicit creates/removals.

**Rollback decision:** a migration checkpoint is tagged with its exact extended physical scope. Its restore uses that scope, including legacy `tree/`, destination escape paths, and original marker absence, rather than ordinary v1 coverage. Preserve current affected bytes before a completed migration rollback. Incomplete migration recovery uses 07's pending plan. Custom and session notes remain unchanged unless explicitly listed in the migration plan.

**Pass:** sparse-only clone, local-only tree, identical copies, empty markers, escaped destinations, differing copies, and existing custom/session areas each have expected outcomes. Applying a clean migration preserves every useful note; rerunning is no-op. Restoring the migration baseline restores old bytes and marker absence and removes only newly created migration destinations. Inject one mid-migration failure and recover.

**Fail:** auto-migration from init/context; success with unresolved conflicts; destination overwrite because it is “derived”; rollback restoring Markdown but leaving a format-1 marker over alpha layout. Never migrate the real dogfood checkout to debug an untested migration.

## 10 — Finish diagnostics and stable CLI behavior

**Deliver:** stable JSON/text renderers and one documented findings table. Use exit 0 for completed success, 1 for findings/migration-needed/strict omissions, 2 for syntax, and 3 for operational/format/conflict/recovery failure. No stack trace or progress text inside JSON stdout; operational errors have a machine-readable result and concise diagnostics. IDs are stable across minor releases.

Freeze finding IDs in a fixture before wiring each check. At minimum distinguish missing required note, malformed/unsupported format, policy error, invalid note name, conflicting alpha notes, broken local link, orphan path note, invalid session, and unavailable/corrupt/pending history. Missing optional path notes and missing history in a fresh clone are normal; corrupt existing history is not. `--explain` describes discovery/history eligibility, never actual Git tracking status.

Health checks observe; they do not fix. Session checks run only for explicitly selected sessions. A durable link that requires a local session is a portability finding. Support a bounded documented subset of local Markdown links for validation (relative inline file links, strip fragment for file existence, skip web/mail URLs); do not add a general Markdown engine or open URLs. Validation cannot certify semantic freshness.

**Pass:** one normal and one failure fixture per command verifies exit code, structured result, and no unexpected writes. `--help`/`version` work outside a repo. Unknown flags/duplicate singleton options are usage errors; repeated `--node` is intentional. All dry-runs, including restore and migrate, leave existing history unchanged and create none when absent.

**Fail:** unsupported format treated as warning-and-continue for mutations; missing optional notes force users to create clutter; default health scanning unrelated session contents; warnings only printed as text while JSON claims success.

## 11 — Verify the package users will actually install

**Deliver:** one framework-dependent `RepoLore.Cli` .nupkg with `PackAsTool=true`, command `repolore`, Core/Infrastructure assemblies included, no separate library packages. Confirm package ID ownership/availability before publication; change one version/identity source if unavailable, not scattered literals. Do not publish a placeholder to reserve a name during implementation.

NuGet install needs a preinstalled .NET SDK and compatible runtime. Document supported .NET 10 environments and disable untested major runtime roll-forward. Tool operation does not depend on repository language. Use a pinned local manifest as the team/CI example and global install only as an alternative.

**Pass:** pack Release; inspect the archive for required assemblies and the absence of repository knowledge, sessions, history, secrets, and test assets. On prepared Windows/Linux/macOS runners, install the actual package into an isolated tool/cache location using a dedicated config with `<clear/>` and only a local feed. Restore the pinned manifest and execute init/context/checkpoint/history/restore/health on temp fixtures. On Linux, repeat the install/runtime smoke with networking blocked and empty tool/package caches; SDK and feed artifacts are pre-staged. An empty feed negative control must fail rather than finding a cached/public copy.

**Fail:** testing only `dotnet run`; hidden public-source fallback; package works only because developer build outputs are in the cwd; publishing assemblies independently to solve a packaging mistake; claiming zero networking by inference from one successful disconnected run. Static guards and a bounded network-call trace provide complementary evidence for tested built-in paths.

## 12 — Exercise the complete workflow and publish a preview

**Deliver:** short installation/internal-feed, daily-use, sessions, format/migration, and recovery instructions. Keep original alpha fixtures frozen. Update both method copies and agent guidance only with implemented behavior. Before dogfood migration, make a verified local backup outside the tool's own recovery store and preserve existing working changes.

Run one scripted acceptance journey on disposable fixtures: clean install → init → apply recommended sessions/history Gitignore entries → durable note → session A/B → checkpoint → default context → selected A context → edit A → checkpoint → promote one verified fact to durable knowledge → review intended Git diff → restore A's previous file → explicit session cleanup. Verify sessions/history are ignored with Git in this test/documented workflow; this does not add a Git subprocess to the shipped CLI. A fresh clone of the durable result must work with no sessions or local history.

**Pass:** the journey is repeatable from a clean directory, with exact expected file hashes and context sentinels. A person following the docs can identify which state is recoverable, which note is provisional, and which files should enter a PR. Publish a prerelease only after the package test; explicitly label any signing/platform limitation.

**Fail:** an agent must know undocumented command order; a Gitignored session leaks into the reviewed diff/package; promotion leaves durable guidance depending on a local-only file; old alpha commands remain recommended for migrated knowledge. Numerical productivity claims are not a gate or a permitted substitute for these checks.

## 13 — Release v1.0.0 with verifiable artifacts

**Deliver:** public license, release notes, support/runtime matrix, vulnerability-reporting route, SBOM, signing/verification procedure, checksum, and the single tool artifact. Keep signing credentials outside source. Tag must match the version embedded in the artifact. No plugins, npm wrappers, separate Core package, or unrequested hosted service.

Build/test/package once for the candidate; sign the package, verify its signature, then calculate hashes and produce release metadata. Re-run the package smoke on the final signed candidate, not a rebuilt unsigned substitute. Document NuGet.org's repository-signing effects when comparing a downloaded package with the author-signed candidate; record hashes for the exact artifacts distributed rather than promising byte identity after a registry adds a signature. See [NuGet signed-package reference](https://learn.microsoft.com/en-us/nuget/reference/signed-packages-reference) and [repository-signing behavior](https://devblogs.microsoft.com/dotnet/Introducing-Repository-Signatures/). Preserve content/version identity and verify the downloaded public package through its trust chain.

**Pass:** required deterministic suites and final package smoke pass on the supported OS matrix. The local approval bundle and internal-feed instructions install without public sources. Signature verification succeeds on the tested platform; the checksum matches the named artifact; package contents/version match the tagged source. A post-publication download installs and reports the expected version. Record any external credential/name/signing blocker; a preview remains a preview until resolved.

**Fail:** publishing despite a preservation failure; hashing before signing and publishing a stale checksum; retagging a published version to different bits; describing the release as certified for banks or as preventing external agents from uploading content. Future fixes get a new version, with compatibility and recovery documented.

## Completion record

Track implementation here or in linked PRs; all entries start incomplete. For each, record the commit/PR, fixture/check command, observed result, and limitations. Do not check off the roadmap because this instruction document exists.

- [x] 01 — Executable boundary and fixtures — implemented and validated; Windows/Linux/macOS CI green (details below)
- [x] 02 — Filesystem paths and knowledge mapping — implemented and validated; Windows/Linux/macOS CI green (details below)
- [x] 03 — Configuration and coverage policies — implemented and validated locally (details below)
- [x] 04 — Durable and session context — implemented and validated locally (details below)
- [x] 05 — Snapshot capture and publication
- [x] 06 — Retention and mutation ownership
- [x] 07 — Restore and interrupted-operation recovery
- [x] 08 — Initialization and method updates
- [ ] 09 — Alpha migration and rollback
- [ ] 10 — Health checks and final CLI contract
- [ ] 11 — Packaged tool and restricted installation
- [ ] 12 — Dogfood and NuGet preview
- [ ] 13 — Signed v1.0.0 release

### Package 01 — local evidence, 2026-09-05

Committed; not published. The [executable foundation note](executable-foundation.md) describes responsibilities and maintenance checks. Delivered Core/Infrastructure/Cli plus three dependency-free executable test projects; SDK 10.0.100, `net10.0`, and lock files; single CLI version source `0.1.0-preview.1`; independent knowledge format 1; real `version`/`--help`; only Cli packable; compiled-capability and resolved-dependency guards. No knowledge command returns placeholder success. No checkpoint or migration was performed on this checkout, and both alpha method copies remain identical.

Checks actually run on macOS arm64 with the SDK installed temporarily at `/tmp/repolore-dotnet`:

- `/tmp/repolore-dotnet/dotnet restore --locked-mode --disable-build-servers` — passed; all six projects restored with cleared package feeds and no NuGet dependencies.
- `/tmp/repolore-dotnet/dotnet build --configuration Release --no-restore --disable-build-servers -m:1` — passed, 0 warnings and 0 errors.
- `/tmp/repolore-dotnet/dotnet run --project tests/RepoLore.Cli.Tests --configuration Release --no-build` — passed, **4/4 checks**. Verified version outside a repository, the six named fixtures unchanged after version/help/invalid commands, exactly the three shipped project libraries in the produced `.deps.json`, and guard rejection/recovery. Nine temporary API probes cover HTTP, DNS, sockets (one in each shipped project), process execution, assembly loading, load contexts, P/Invoke, native-library loading, and reflection. A tenth probe adds a non-BCL assembly reference and fails with the dependency diagnostic. Removing probes restores a successful build.
- Fixtures: `minimal-v1`, `two-sessions`, `alpha-sparse-only`, `alpha-local-only`, `alpha-conflict`, `history-failures` (Cli.Tests), plus `expected-mappings.tsv` (Core.Tests). Distinct session roots and the materialized Gitignore template are checked. Their later context/mapping/recovery behavior is not implemented or claimed.
- `git diff --check` and `cmp method.md _repolore/method.md` — passed. Verified synthetic session fixture files are not Gitignored; real checkout sessions/history are ignored.

Initial sandboxed SDK startup stalled; validation used an approved build outside the sandbox. An initial test-host PATH lookup and a fixture Gitignore packaging issue were fixed before the final passing run. The build tool is outside the shipped application boundary.

CI gate resolved: `.github/workflows/ci.yml` runs locked restore/build and all three test suites on Windows, Linux, and macOS; the matrix is green. Package 01 is complete. No later package, actual tool install, network trace, or release is claimed complete.

### Package 02 — local evidence, 2026-09-06

Delivered the shared path resolver and the one-note-per-directory mapping. The [paths-and-mapping note](paths-and-mapping.md) records responsibilities and the span-lowering pitfalls that trip the build guard. Core's `KnowledgePathMapper` is pure: each directory maps to a note named after it, files get no notes, and the root maps to `root.md`. Infrastructure's `RepositoryPathResolver` validates rooted targets, rejects `..`/rooted/UNC/symlink escapes, and detects case/Unicode aliases on the destination filesystem.

Checks actually run on macOS arm64 with the Rider SDK on PATH (`~/.dotnet`, 10.0.100):

- `dotnet build --configuration Release --no-restore` — passed, 0 warnings and 0 errors.
- `dotnet run --project tests/RepoLore.Core.Tests --configuration Release --no-build` — passed, **3/3 checks**; `tests/RepoLore.Infrastructure.Tests` — **3/3 checks**; `tests/RepoLore.Cli.Tests` — **4/4 checks**.
- Package 02 checks: `RepoLore.Core.Tests` asserts the authored `expected-mappings.tsv` (`src/`, `src/sub/`, `a.md/`) encodes each directory note and decodes it round-trip, the root maps to `root.md`, and non-directory note paths (`src/client.cs.md`, `foo/bar.md`, a bare `src.md`, etc.) produce findings; `RepoLore.Infrastructure.Tests` asserts the resolver rejects `..`/rooted/UNC/backslash targets and a symlinked ancestor (while a `repo-other` sentinel outside the root stays untouched), write-alias detection agrees with what the filesystem actually resolves, and over-long note names are refused before `Path.GetFullPath`.

During development the build guard rejected `System.Runtime.CompilerServices.Unsafe`/`MemoryMarshal` imported by `Encoding.UTF8.GetByteCount(string)`, `Path.GetRelativePath`, and multi-char `Split(char, char)`; each was replaced with a plain deterministic equivalent (char-count length bound, substring walk, single-char split).

CI gate resolved: the Windows/Linux/macOS matrix is green. Package 02 is complete. Package 03 (configuration and coverage policies) is next.

### Package 03 — local evidence, 2026-09-26

Delivered the format/config parser and the one bounded matcher plus the two coverage policies, all pure in Core. The [configuration-and-policies note](configuration-and-policies.md) records responsibilities. `Json.cs` provides a hand-written JSON parser/writer (duplicate-key and malformed-input detection, unknown-field retention); `ConfigParser` validates `formatVersion` (required, must equal 1; higher fails with an upgrade instruction) and the optional `history` object (`enabled`/`maxBytes`/`exclude` defaults, negative/non-integral/overflowing budgets and invalid rules rejected); `RuleSet` implements the anchored grammar; `SourceDiscoveryPolicy` layers hard exclusions → defaults (`**/bin/`, `**/obj/`, `**/node_modules/`, `**/build/`, `**/vendor/`) → user `_repoloreignore`; `HistoryCoveragePolicy` decides eligibility over regular files independently of Git and source discovery. Components are split into responsibility folders (`Format/`, `Json/`, `Mapping/`, `Matching/`, `Configuration/`, `Policies/`) with one-to-one namespace mapping, and the Core tests mirror that layout.

Checks actually run on macOS arm64 with the Rider SDK on PATH (`~/.dotnet`, 10.0.100):

- `dotnet restore --locked-mode --disable-build-servers` — passed; all six projects restored, no package changes.
- `dotnet build --configuration Release --no-restore` — passed, 0 warnings and 0 errors (Core compiles under the boundary guard without tripping it; the Cli guard test also recompiles copied `src/` with probes).
- `dotnet run --project tests/RepoLore.Core.Tests --configuration Release --no-build` — passed, **19/19 checks**. New checks: the frozen `ignore-rules.txt`/`expected-matcher.tsv` table (root `build` vs `**/build/`, `build/a`, `src/build/a`, `!src/build/a.md` re-inclusion through an excluded parent, `!vendor/keep.md` override, `dist/` directory-only, `docs/*.tmp` no cross-segment `*`, `notes/??.md` `?`, `\#`/`\!` literal leading, comments/blank lines); invalid rules (`ab**cd`, `a**`, `**a`, `a[bc]`, `a\b`, `a//b`, `\x`, bare `!`) rejected with line numbers; last-match ordering; `**` zero-or-more segments; source discovery (hard exclusions win over `!`, defaults, user override, `HasNegation`); history coverage (Gitignored session captured by default, `history.exclude` narrows only that coverage, `.history/`/symlinks/directories/non-Markdown excluded); and config parsing (defaults, full parse, malformed JSON is an error, duplicate keys, wrong types, negative/non-integral/overflowing budgets, invalid `history.exclude`, unknown/higher format versions, missing `formatVersion`, unknown-field round-trip preservation).
- Regression: `tests/RepoLore.Infrastructure.Tests` — **3/3 checks**; `tests/RepoLore.Cli.Tests` — **4/4 checks**. All pass unchanged.
- `git diff --check` and `cmp method.md _repolore/method.md` — passed; the two method copies remain identical.

No policy change deletes a file (policies are pure decision functions). "Missing marker is alpha (format 0)" remains a filesystem-presence rule to be wired by init/migration (packages 08–09). CI/platform validation is not yet run for this package; local evidence only. Package 04 (durable and session context) is next.

### Package 04 — local evidence, 2026-09-26

Delivered the read-only `path`, `context`, and `tree` commands with alpha read support and explicit session scope. The pure `RepoLore.Core.Context` components (`SessionId`, `KnowledgeNodePath`, `ContextSelector`, `BlockEstimator`, `NoteContent`, `NoteReadResolver`) implement selection, dedup, budget estimation, session-id validation, and sparse/alpha-copy resolution; the CLI orchestrates and reads via the existing `RepositoryPathResolver`. The [durable-and-session-context note](durable-and-session-context.md) records responsibilities and the `record`-type build-guard pitfall.

Checks actually run on macOS arm64 with the Rider SDK on PATH (`~/.dotnet`, 10.0.100):

- `dotnet restore --locked-mode --disable-build-servers` — passed; all projects up to date, no package changes.
- `dotnet build --configuration Release --no-restore` — passed, 0 warnings and 0 errors (the new Core `Context/` and Cli command files compile under the boundary guard; `record` types were avoided because the guard rejects the generated `System.Type`).
- `dotnet run --project tests/RepoLore.Core.Tests --configuration Release --no-build` — passed, **36/36 checks**. New checks: session-id accept/reject/reserved/limit; selection order (method → root/ancestors/target), root-only target, session root + named nodes, dedup, node-only, invalid/cross-session/history/outside/non-Markdown node rejection, unsupported source-target segments; canonical block format and `ceil(length/4)` charge, skip-non-fitting-and-continue, fits-nothing; BOM/CRLF/trim normalization, invalid-UTF-8 finding, empty marker; sparse-preferred/tree-fallback/conflict resolution.
- Regression: `tests/RepoLore.Infrastructure.Tests` — **3/3 checks**; `tests/RepoLore.Cli.Tests` — **19/19 checks** (4 existing foundation checks unchanged plus 15 new process-level checks).
- Package 04 checks (Cli.Tests, process-level, frozen `two-sessions`/`minimal-v1`/`alpha-sparse-only`/`alpha-local-only`/`alpha-conflict` fixtures plus an inline budget fixture): default durable context excludes both sessions; selecting A includes only A's root and named nodes; missing session/note findings exit 1; invalid `--session` exits 2 even with `--budget-tokens 1`; cross-session node exits 2; no-selector/zero/negative budget exit 2; duplicate nodes appear once; an oversized ancestor is omitted while a later fitting target note is still included (plain and `--json` agree); `--strict` exits 1 on budget omission while returning the partial result; alpha sparse-only/tree-only read correctly and alpha-conflict exits 3; `path` lists status; `tree` excludes sessions by default, lists IDs via `--start _repolore/sessions/`, and lists a session's notes when started inside one; a read-only batch leaves file contents/hashes/mtimes unchanged.
- `git diff --check` and `cmp method.md _repolore/method.md` — passed; the two method copies remain identical (unchanged).

Selection, estimation, session-id validation, and alpha resolution are pure; no read, preview, `path`, `context`, or `tree` invocation writes or checkpoints (invariant 8). Multiple selected sessions, recursive link loading, and health-check link validation remain out of scope (roadmap: single session per read; links reported later by health-check). CI/platform validation is not yet run for this package; local evidence only. Package 05 (snapshot capture and publication) is next.

### Package 05 — local evidence, 2026-09-27

Delivered `checkpoint` and `history` over plain files plus the basic exclusive writer guard. The pure `RepoLore.Core.Snapshot` components (`HistoryVersion`, `ManifestId`, `ManifestCodec`, `SnapshotDiffer`, and the manifest/scope/entry model) handle monotonic IDs, strict manifest decode, and no-op/diff classification; `RepoLore.Infrastructure.History` owns the clock, SHA-256 hashing, the `write.lock` guard, the object/checkpoint stores, the eligible-file walker, and the two-pass capture engine. `RepoLoreConfig` now retains the raw `history.exclude` rules so manifests record exact scope. The [snapshot-capture note](snapshot-capture.md) records responsibilities and the fault-injection hooks.

Checks actually run on macOS arm64 with the Rider SDK on PATH (`~/.dotnet`, 10.0.100):

- `dotnet build --configuration Release --no-restore` — passed, 0 warnings and 0 errors (the new Core `Snapshot/` and Infrastructure `History/` files compile under the boundary guard; no `record` types were introduced).
- `dotnet run --project tests/RepoLore.Core.Tests --configuration Release --no-build` — passed, **48/48 checks**. New checks: manifest-id format/parse/reject; manifest encode/decode round-trip and malformed-input rejection (wrong types, unknown history version, negative id/size/maxBytes, unknown repoloreJson status, malformed scope/files); snapshot diff (first-checkpoint all-added, unchanged no-op, add/change/remove classification, scope-only change, exclude-rule and repoloreJson-status changes).
- `dotnet run --project tests/RepoLore.Infrastructure.Tests --configuration Release --no-build` — passed, **17/17 checks**. New checks: object store/reuse/validate and corrupt-object refusal; checkpoint store publish/highest-id, incomplete-file ignore, malformed-manifest error, empty history; writer-lock acquire/release and second-writer refusal; capture engine first publish, no-op, add/change/delete, changed-during-capture failure (no publish), interrupted-before-publish leaves previous valid, shared objects.
- `dotnet run --project tests/RepoLore.Cli.Tests --configuration Release --no-build` — passed, **29/29 checks**. New checks (frozen `history-failures` fixture plus inline fixtures): add/change/delete as three distinguishable manifests with recoverable original bytes; unchanged no-op; mtime-only no-op; same-length/same-mtime byte change captured; distinct paths with identical bytes share one object; scope/exclusion change recorded when visible hashes match; disabled history exits 3 and history stays readable; second writer refused (cross-process); alpha conflict captured as separate paths; incomplete files ignored while a malformed completed manifest fails.
- `git diff --check` and `cmp method.md _repolore/method.md` — passed; the two method copies remain identical (unchanged).

Retention/eviction (`history.maxBytes` counting, oldest-first eviction, unreferenced-object reclamation) and cross-process lock/crash validation remain for package 06; package 05 records the budget in scope but does not enforce it. The `checkpoint`/`history` commands write only under the writer lock and publish the manifest last. CI/platform validation is not yet run for this package; local evidence only. Package 06 (retention and mutation ownership) is next.

### Package 06 — local evidence, 2026-09-27

Delivered retention/eviction against `history.maxBytes` (default 209715200) and cross-process/crash validation of the package-05 writer guard. New `RepoLore.Infrastructure.History.HistoryCleanup` (`CleanupResult` plus byte counting, eviction, and reclamation) reuses `ObjectStore`/`CheckpointStore`. `CheckpointEngine.Capture` preflights that the new snapshot alone fits (`SnapshotBytes > maxBytes` fails before publishing, exit 3), publishes the manifest, then evicts oldest-first (never the latest) and reclaims objects no surviving manifest references, counting shared objects once. `checkpoint` reports evicted/reclaimed/retained bytes and warns on stderr when the budget was not achieved. Core is unchanged; the [retention note](retention-and-mutation-ownership.md) records responsibilities and the hand-written UTF-8 byte counter (avoids the `Encoding.UTF8.GetByteCount` build-guard trip).

Checks actually run on macOS arm64 with the Rider SDK on PATH (`~/.dotnet`, 10.0.100):

- `dotnet build --configuration Release --no-restore` — passed, 0 warnings and 0 errors.
- `dotnet run --project tests/RepoLore.Infrastructure.Tests --configuration Release --no-build` — passed, **25/25 checks**. New checks: oldest-first eviction + object reclamation; shared-object retention; latest-never-evicted; failed-delete reports `BudgetOk=false` without corrupting history; exact-at-budget success; one-byte-over failure leaving prior state usable; shrinking the budget evicts on the next capture; a killed lock owner does not block later writes (spawned `--hold-lock` holder, killed it, re-acquired).
- `dotnet run --project tests/RepoLore.Cli.Tests --configuration Release --no-build` — passed, **31/31 checks**. New checks: over-budget checkpoint exits 3 and leaves the previous checkpoint usable; checkpoint reports eviction when the budget shrinks.
- Regression: `tests/RepoLore.Core.Tests` — **48/48 checks** (unchanged; no Core change).
- `git diff --check` and `cmp method.md _repolore/method.md` — passed; the two method copies remain identical.

Retention counts retained manifest and object bytes exactly (objects deduplicated); `tmp/`, `write.lock`, and pending controls are not charged. A failed cleanup preserves validity and retries on a later mutation, never a read. The restore-target pinning and pre-operation "undo pair" preflight from the roadmap remain for package 07 (restore and interrupted-operation recovery), which is next. CI/platform validation is not yet run for this package; local evidence only.

### Package 07 — local evidence, 2026-09-27

Delivered `restore <id> [--path <path>] [--dry-run]` as a pure plan first, then a guarded application, with an interrupted-operation recovery route. New pure `RepoLore.Core.Restore` components (`RestorePlan`, `RestoreAction`, `RestorePlanner`, `PendingTransaction`, `PendingPlanEntry`, `PendingCodec`) compute the plan and model/codec the durable pending record; `RepoLore.Infrastructure.History.RestoreEngine` (with `RestoreResult`, `RestoreException`, and `PendingStore`) orchestrates plan/apply/recover while reusing `ObjectStore`/`CheckpointStore`/`CheckpointEngine`/`HistoryCleanup`/`WriterLock`/`HistoryCoveragePolicy`/`ConfigParser`/`RepositoryPathResolver`. The [restore-and-recovery note](restore-and-recovery.md) records responsibilities and the retention-pinning extension.

Behavior: the plan is the candidate union of saved and currently-eligible paths, restricted to paths eligible under both saved `scope.exclude` and current `history.exclude`; saved absence deletes only inside saved coverage, and an explicit `--path` excluded on either side fails with a scope explanation. Unknown ids and corrupt target objects fail before any authored mutation. No-op restore (complete plan unchanged) reports no changes and creates no checkpoint or pending record. Apply writes `.history/pending.json` (target id, protected pre-operation id, historyVersion, frozen config/coverage/budget, and the affected before/after plan) before the first replacement, stages each write as a temp-file rename, rechecks each file against its recorded before-state, preflights `HistoryCleanup.ProtectedBytes({pre-operation, resulting, target}) ≤ maxBytes`, then publishes the post-checkpoint and clears pending. A pending record makes `checkpoint` and non-matching `restore` refuse with the recovery id, while `history`/`context`/`path`/`tree` stay available. `restore <pre-operation-id>` recovers exactly the before-images, refuses a fresh unrelated edit instead of overwriting it, confirms/publishes the recovered before-state checkpoint under the frozen policy, and only then clears pending; no nested recovery. `HistoryCleanup.Clean(maxBytes, protectedIds)` and `CheckpointEngine.Capture(..., protectedIds)` implement the retention pinning.

Checks actually run on macOS arm64 with the Rider SDK on PATH (`~/.dotnet`, 10.0.100):

- `dotnet restore --locked-mode --disable-build-servers` — passed; all six projects restored, no package changes.
- `dotnet build --configuration Release --no-restore` — passed, 0 warnings and 0 errors (new Core `Restore/` and Infrastructure/CLI files compile under the boundary guard; no `record` types).
- `dotnet run --project tests/RepoLore.Core.Tests --configuration Release --no-build` — passed, **62/62 checks**. New checks (14): planner add/replace/delete/unchanged classification; identical-state no-op; currently-excluded path never deleted; saved-excluded path never deleted when newly covered; `--path` single-file replace/add/delete/absent-in-both; `--path` excluded on current/saved side fails with a scope explanation; pending codec round-trip and malformed/missing/wrong-type/negative/unknown-version rejection.
- `dotnet run --project tests/RepoLore.Infrastructure.Tests --configuration Release --no-build` — passed, **38/38 checks**. New checks (13): restore to an old snapshot then restore its pre-operation id undoes it byte-for-byte; excluded file untouched; unknown id and corrupt target object fail before mutation; disabled history refuses; no-op restore leaves no pending/checkpoint; failure before the first replacement, between two replacements, and after writes but before the final checkpoint each report the same recovery id (2) and recover; restart-and-recover restores the exact before-state; a fresh unrelated edit during recovery is detected not overwritten; `--path` restores one file; restore preflights the protected set against the budget before mutating.
- `dotnet run --project tests/RepoLore.Cli.Tests --configuration Release --no-build` — passed, **38/38 checks**. New checks (7, process-level): restore/undo; `--dry-run` lists add/replace/delete/unchanged and writes nothing (history and files unchanged); unknown id exits 3; `--path` restores one file; `checkpoint` and `restore` refuse with the recovery id while pending; `restore <pre-operation-id>` recovers the before-state and clears pending; a fresh edit during recovery exits 3 and stays pending.
- `git diff --check` and `cmp method.md _repolore/method.md` — passed; the two method copies remain identical.

The shipped CLI performs no network/process/telemetry operations; the boundary guard still passes. Migration (09), health-check findings (10), and the packaged install (11) remain out of scope. CI/platform validation is not yet run for this package; local evidence only. Package 08 (initialization and method updates) is next.

### Package 08 — local evidence, 2026-09-29

Delivered `init` and `init --update-method`. New `RepoLore.Infrastructure.History.InitEngine` (`InitDirKind`, `InitException`, `InitIncompleteException`, `InitResult`) classifies `_repolore/` as absent/empty/alpha/v1 and reuses `CheckpointEngine`/`PendingStore`/`ObjectStore`/`CheckpointStore`; `RepoLore.Cli.Commands.InitCommand` refuses alpha before taking the writer lock. The v1 method and root templates ship as two `const string` values in `RepoLore.Cli.Embedded.EmbeddedTemplates` (a plain constant rather than an embedded resource, since `System.Reflection` is forbidden). Fresh init publishes the `{"formatVersion":1}` marker atomically before any authored template, creates only missing `method.md`/`root.md`/empty `sparse-tree/`, then captures the first checkpoint. `--update-method` reuses the package-07 pending transaction unchanged: pre-op capture, `pending.json` with a single `_repolore/method.md` before/after entry, staged rename, post-checkpoint with the pre-op id pinned, clear pending; interruption reports the recovery id and the existing `restore <pre-operation-id>` route recovers. The [initialization-and-method-updates note](initialization-and-method-updates.md) records responsibilities and the `Encoding.UTF8.GetBytes` build-guard avoidance.

Checks actually run on macOS arm64 with the Rider SDK on PATH (`~/.dotnet`, 10.0.100):

- `dotnet build --configuration Release --no-restore` — passed, 0 warnings and 0 errors (the new Infrastructure/CLI files compile under the boundary guard; no `record` types).
- `dotnet run --project tests/RepoLore.Infrastructure.Tests --configuration Release --no-build` — passed, **49/49 checks**. New checks (11): empty directory initializes with marker/templates/tree and one baseline; second init changes nothing; preexisting root/config/custom notes survive exactly; first-checkpoint failure is visible (paths reported, no checkpoint) and retryable; alpha content without a marker is refused and never emptied; a user-edited method is never replaced during ordinary init; method update rewrites the method and can be undone to exact old bytes; unchanged embedded bytes are a no-op; method update with disabled history refuses; method update interrupted before write leaves pending and recovers; disabled history reports protection disabled and skips the checkpoint.
- `dotnet run --project tests/RepoLore.Cli.Tests --configuration Release --no-build` — passed, **45/45 checks**. New checks (7, process-level): empty directory initializes with one baseline and no source placeholders; second init changes nothing; preexisting root/config/custom notes survive; a nonempty alpha sparse-only clone is never emptied (exit 3, no marker written); a user-edited method is never replaced; method update can be undone to exact old bytes via `restore 1`; disabled history creates files and states protection disabled. The existing foundation test that asserted `init` was an unknown command (exit 2) was updated to drop `init` from the rejected-command list; the remaining rejected commands still assert exit 2 and unchanged fixtures.
- Regression: `tests/RepoLore.Core.Tests` — **62/62 checks** (unchanged; no Core change).
- `git diff --check` and `cmp method.md _repolore/method.md` — passed; the two method copies remain identical (unchanged).

`init` never inspects source, creates empty nodes, starts a session, regenerates sparse-tree, or rewrites `.gitignore`; it reports the recommended `_repolore/sessions/` and `_repolore/.history/` exclusions. A failed init deletes nothing (the marker remains so retry completes). The repo-root and `_repolore/` method copies in this checkout remain alpha; only the packaged `EmbeddedTemplates.Method` ships the new v1 method, and the checkout's method copies stay unchanged until migration (09). CI/platform validation is not yet run for this package; local evidence only. Package 09 (alpha migration and rollback) is next.

### Test stack — policy change, 2026-10-03

Test projects may now reference an approved test stack: xUnit (`2.9.3`), FluentAssertions (`8.11.0`), FsCheck (`3.4.0`), Newtonsoft.Json (`13.0.4`), and CliWrap (`3.10.5`, Cli tests only). `NuGet.Config` gained a single `nuget.org` source (the `<clear />` remains first). The shipped projects stay BCL-only — the build guard is per-project and unchanged, so test-only references never weaken the shipped boundary. This is staged: the library packages are referenced now; `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio` and the `dotnet run` → `dotnet test` switch land with the test rewrite, because the adapter generates its own entry point and cannot coexist with the current hand-rolled `Program.cs` files (CS7022 under `TreatWarningsAsErrors`). Evidence so far: `dotnet restore --locked-mode` resolves all packages; `dotnet build --configuration Release --no-restore` is 0 warnings/0 errors; the three `dotnet run` suites still pass (Core 62/62, Infrastructure 49/49, Cli 45/45). The test-method rewrite and CI update remain outstanding.

**Core migrated to xUnit — 2026-10-03.** `RepoLore.Core.Tests` no longer has a `Program.cs` or `TestRunner.cs`; every `TestRunner.Check("name", () => …)` is now a named `[Fact]` using FluentAssertions (`Should()`/`Throw<T>()` in place of `Equal`/`True`/`Throws`/`Capture`), with no assertion weakened or dropped. Added `Microsoft.NET.Test.Sdk` 18.10.1 and `xunit.runner.visualstudio` 4.0.0 (standard `PrivateAssets`/`IncludeAssets`); `OutputType` was dropped from the csproj so the adapter's generated entry point no longer collides. New differential test `tests/RepoLore.Core.Tests/Json/NewtonsoftParityTests.cs` generates JSON trees with FsCheck (`Gen.Sized`/`Gen.OneOf`/`Gen.ListOf` over a string alphabet including escapes and non-ASCII) and asserts the hand-rolled `JsonParser`/`JsonWriter` agree with Newtonsoft.Json: `JToken.DeepEquals` of the parsed model, and write→reparse round-trip stability plus Newtonsoft readability. Checks: `dotnet restore --locked-mode` resolves; `dotnet build --configuration Release --no-restore` 0 warnings/0 errors; `dotnet test --no-build --configuration Release` **64/64 passed** (62 ported checks + 2 property tests). Fixtures exercised: `tests/RepoLore.Core.Tests/Fixtures/expected-mappings.tsv`, `ignore-rules.txt`, `expected-matcher.tsv`. The two other suites still pass unchanged (Infrastructure 49/49, Cli 45/45 via `dotnet run`). Infrastructure and Cli migrations plus the CI switch remain outstanding.
