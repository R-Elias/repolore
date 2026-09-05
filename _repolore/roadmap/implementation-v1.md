# First Release — Implementation Work Packages

Status: **implementation instructions, no CLI work completed**. Target: a preview followed by v1.0.0 of the single `RepoLore.Cli` NuGet tool. This guide decomposes the [roadmap](roadmap.md); [invariants](../product/invariants.md) and the [session contract](../product/sessions.md) still apply. It resolves earlier open implementation choices below. Do not copy alpha mirror-generation behavior into v1.

## How to execute this plan

Complete one work package at a time, in dependency order. Each should produce a reviewable change with its own passing gate. A compiling stub, happy-path demonstration, or updated checkbox is not completion. Do not start destructive commands until the preservation gates pass. Preserve unrelated working changes.

Use a single test project with fixture files and a narrow fault-injecting filesystem wrapper. No large test framework, research program, daemon, or plugin infrastructure. Tests may invoke the CLI as a process; the shipped CLI must not spawn processes. Inject time and IDs rather than asserting wall-clock timestamps or sleeping in tests.

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

**Deliver:** `Core`, `Infrastructure`, `Cli`, and one test project. Only Cli is packable. Set the .NET 10 target, pin SDK/package versions, and expose a real `version` command carrying CLI version and supported knowledge formats. Keep format version independent of package version.

Build-time guards cover all shipped projects: no networking APIs, process execution, dynamic loading/native interop used to evade guards, or third-party runtime dependencies. Core plans operations through narrow interfaces; Infrastructure owns physical IO. Add abstractions only as the following packages need them. Read console arguments/environment at the boundary and pass values explicitly.

Create small named fixtures reused below: `minimal-v1`, `two-sessions`, `mapping-collisions`, `alpha-sparse-only`, `alpha-local-only`, `alpha-conflict`, and `history-failures`. Put distinctive sentinel text in unrelated files so accidental reads/writes are observable. Author expected results independently of the implementation under test.

**Pass:** builds/tests on Windows, Linux, macOS; `version` needs no repository and writes nothing. A temporary forbidden API reference makes the guard fail, then removing it restores green. Inspect the produced runtime dependency list, not only project declarations.

**Fail:** empty handlers return success; tests require network access; public Core package or plugin interfaces appear; CI only tests one OS while claiming all three. No requirement for a separate Guard/Perf/Compat project.

## 02 — Freeze safe paths and an injective knowledge mapping

**Deliver:** one shared path resolver for reads, writes, manifests, and migration. CLI paths are repo-root-relative; absolute paths may be accepted only if canonicalized inside the root. Knowledge flags use `_repolore/...`. Reject `..` escapes, rooted manifest paths, drive/UNC substitutions, and symlink/reparse traversal, including an existing ancestor of a nonexistent destination. A string prefix check is insufficient (`repo-other` is not inside `repo`).

Do not infer case behavior solely from OS name. Detect actual directory/file aliases when planning writes; refuse ambiguous case/Unicode aliases on the destination filesystem. Preserve source spelling. Report unsupported filenames/lengths before writing rather than silently truncating or normalizing names. A checkpoint and restore preserve bytes; text rendering may tolerate UTF-8 BOM/CRLF without rewriting them.

**Mapping decision:** retain ordinary alpha mappings, but escape reserved names deterministically using UTF-8 lowercase hex. Mapping depends on the path string, never on which sibling files happen to exist:

1. For each source directory component `D`, use `E(D) = ~d-<hex(D)>` if it starts with `~` or ends with `.md` (case-insensitive suffix check); otherwise `E(D) = D`.
2. A directory note is `sparse-tree/E(parent)/E(D)/E(D).md`.
3. A file note is `sparse-tree/E(parent)/F.md`. Normally `F` is the original filename. Use `F = ~f-<hex(filename)>` if the filename starts with `~` or equals the parent's encoded basename ignoring case. Root-level files only need the `~` rule because the knowledge root lives outside the tree.
4. Decode only valid canonical escape forms; malformed or noncanonical reserved names produce a finding, not a guessed path. Keep mapping knowledge in one component.

This prevents both directory-note/file-note collisions and file-note/mirrored-directory collisions. Examples:

| Source target | Path under `sparse-tree/` |
|---|---|
| `src/` | `src/src.md` |
| `src/client.cs` | `src/client.cs.md` |
| `src/src` | `src/~f-737263.md` |
| `src/~src` | `src/~f-7e737263.md` |
| file `a` | `a.md` |
| directory `a.md/` | `~d-612e6d64/~d-612e6d64.md` |

**Pass:** table-driven encode/decode round trips, nested escaped components, unknown targets with directory trailing slash, and real filesystem alias/escape checks. Adding/removing a sibling never relocates another note. `a` and directory `a.md/` coexist. Sentinels outside the root remain unread/unmodified after malicious target/manifest inputs.

**Fail:** use source existence to resolve ambiguous alpha authorship; universal case-folding merges two Linux files; only the `src/src` collision is tested. Escaping can make names too long: refuse affected writes with a path-specific error, do not introduce hidden hash-only mappings.

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

Freeze finding IDs in a fixture before wiring each check. At minimum distinguish missing required note, malformed/unsupported format, policy error, mapping collision, conflicting alpha notes, broken local link, orphan path note, invalid session, and unavailable/corrupt/pending history. Missing optional path notes and missing history in a fresh clone are normal; corrupt existing history is not. `--explain` describes discovery/history eligibility, never actual Git tracking status.

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

- [ ] 01 — Executable boundary and fixtures
- [ ] 02 — Filesystem paths and knowledge mapping
- [ ] 03 — Configuration and coverage policies
- [ ] 04 — Durable and session context
- [ ] 05 — Snapshot capture and publication
- [ ] 06 — Retention and mutation ownership
- [ ] 07 — Restore and interrupted-operation recovery
- [ ] 08 — Initialization and method updates
- [ ] 09 — Alpha migration and rollback
- [ ] 10 — Health checks and final CLI contract
- [ ] 11 — Packaged tool and restricted installation
- [ ] 12 — Dogfood and NuGet preview
- [ ] 13 — Signed v1.0.0 release
