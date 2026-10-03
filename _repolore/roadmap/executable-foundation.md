# Executable foundation — package 01

The .NET foundation exposes `version` and `--help` only. Unknown commands and extra arguments exit 2; no knowledge handler is wired. `version` reports the CLI SemVer from `Directory.Build.props` and the independent knowledge format constant from `RepoLore.Core/Format/KnowledgeFormat.cs`. Reporting format 1 identifies the target contract; it does not imply context, parsing, or migration is implemented.

## Responsibilities and checks

- `src/RepoLore.Core/` holds pure planning, now split into responsibility folders: `Format/`, `Json/`, `Mapping/`, `Matching/`, `Configuration/`, `Policies/`.
- `src/RepoLore.Infrastructure/` is intentionally empty until physical IO is needed. Its assembly is already referenced by the CLI. Add narrow interfaces and failure injection alongside their first tested operation.
- `src/RepoLore.Cli/` handles arguments and console output. It alone is packable as `RepoLore.Cli`; the package installation/release gates remain pending.
- `build/CheckRuntimeBoundary.cs` is an MSBuild inline task, not shipped runtime code. `Directory.Build.targets` checks compiled references in all three shipped projects and rejects network, process, reflection/dynamic loading, and native interop capabilities. Broad reflection restrictions are deliberate: evaluate a narrow justified allowance with a negative fixture before relaxing them. This is a regression guard, not a sandbox for hostile code.
- The dependency target permits only framework references and the known Core/Infrastructure project references in shipped projects. `global.json` pins SDK 10.0.100 with roll-forward disabled. Commit generated package lock files. Test projects are the sole exception to the no-package rule: they may reference the approved test stack (xUnit, FluentAssertions, FsCheck, CliWrap, Newtonsoft.Json), which restores through `NuGet.Config`'s single `nuget.org` source. The shipped binary remains BCL-only; the build guard is per-project (`EnforceRuntimeBoundary`) and untouched by test-only references.
- `tests/RepoLore.Core.Tests/`, `tests/RepoLore.Infrastructure.Tests/`, and `tests/RepoLore.Cli.Tests/` are three executable test projects currently run with `dotnet run`. Core tests cover the path mapper, Infrastructure tests the resolver, and Cli tests launch the CLI as a process and inspect exit/output, file hashes/existence/mtimes, emitted runtime dependencies, and temporary forbidden references in copied projects. The copied projects and fixture workspaces are deleted after each test. Test-framework packages are being introduced incrementally: library packages land first; the xUnit runner/adapter (`Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio`) and the `dotnet run` → `dotnet test` switch land when the hand-rolled `Program.cs` entry points are removed, because the adapter generates its own entry point and cannot coexist with them.

Run from the repository root with the pinned SDK installed:

```sh
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet run --project tests/RepoLore.Core.Tests --configuration Release --no-build
dotnet run --project tests/RepoLore.Infrastructure.Tests --configuration Release --no-build
dotnet run --project tests/RepoLore.Cli.Tests --configuration Release --no-build
```

Build/test tooling is outside the shipped application's privacy boundary. CI disables SDK telemetry and runs the same checks on Windows, Linux, and macOS. Local results and outstanding platform evidence belong in the [completion record](implementation-v1.md#completion-record).

## Frozen starting fixtures

`tests/RepoLore.Cli.Tests/Fixtures/` contains `minimal-v1`, `two-sessions`, `alpha-sparse-only`, `alpha-local-only`, `alpha-conflict`, and `history-failures`. `tests/RepoLore.Core.Tests/Fixtures/expected-mappings.tsv` holds the directory-note expectations. Each fixture includes unrelated sentinel bytes. Session files here are synthetic, committed test inputs; real checkout sessions and history are root-anchored Git exclusions. The fixture copier materializes `gitignore.template` as `.gitignore`, so synthetic session files remain committable in the source fixture. Future packages must add assertions for their behavior; fixture existence is not evidence those packages work.

The dogfood knowledge base still follows the alpha method. No migration, checkpoint protection, source discovery, or session commands exist yet. Both method copies remain unchanged and identical. This directly authored roadmap note records implementation knowledge without regenerating a sparse tree that may contain the checkout's only alpha notes.
