# src

Three shipped projects, one direction of dependency:

- `RepoLore.Core`: pure logic. No file system, no process, no clock.
- `RepoLore.Infrastructure`: every file-system effect (history, restore, init, migration, health checks). Depends on Core only.
- `RepoLore.Cli`: argument parsing, commands, text and JSON rendering, exit codes. Depends on both; it is the packaged tool.

Put logic in the lowest layer that can hold it. Commands must not carry their own copy of path, policy or recovery rules: reuse the shared components.

## The build guard

The shipped projects set `EnforceRuntimeBoundary`, and `Directory.Build.targets` then fails the build with `RLBOUNDARY:` when:

- any `PackageReference` exists (only the SDK's native compiler packages are allowed, and only when `PublishAot` is true);
- a reference is neither the .NET framework nor Core or Infrastructure;
- after compiling, `build/CheckRuntimeBoundary.cs` finds a use of networking (`System.Net`), `Process`, `Activity`, reflection, `Activator`, `Type`, `AppDomain`, `Delegate`, `Unsafe`, dynamic call sites, `System.Linq.Expressions`, assembly loading, interop, or a native method.

This is what backs the "local, offline, no subprocess" claim, so do not work around it. It also means shipped code has no `System.Text.Json`: JSON is parsed and written by hand in `Core/Json`. Test projects are exempt and may use the approved test packages (`ENGINEERING.md`, rule 9).

## Frozen for 1.x

Scripts and agents rely on these; changing one is a breaking change, so a 2.0:

- exit codes `0` success, `1` findings, `2` usage, `3` failure;
- finding and error IDs: `tests/RepoLore.Cli.Tests/Fixtures/expected-ids.tsv` is the frozen list, and every ID must be asserted by a scenario;
- JSON field names and meaning, options and what they do;
- knowledge format 1 and the history format, which every 1.x reads;
- what the scenarios tagged `contract` pin: what `tree` lists and in what order, the checkpoint time format, the rule syntax, the context budget's order, and the ids that `restore`, `migrate` and `init --update-method` report.

Allowed in 1.x: new options, new JSON fields, new findings for real defects, and different human-readable wording. A new ID goes into `expected-ids.tsv` together with a scenario that asserts it.

## Before changing behavior

Read the roadmap note for the area ([executable foundation](../../roadmap/executable-foundation.md), [paths and mapping](../../roadmap/paths-and-mapping.md), [health and CLI contract](../../roadmap/health-and-cli-contract.md)); its pass/fail gates are required. The three assemblies keep their names inside the package (`RepoLore.Cli.dll`, ...) although the package ID is `RepoLore`.
