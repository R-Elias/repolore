# tests

One test project per shipped assembly: `RepoLore.Core.Tests` (in-memory; its folders mirror Core's), `RepoLore.Infrastructure.Tests` (temp directories, injected clock and delete function) and `RepoLore.Cli.Tests` (the real CLI). Test code is code ([engineering policy](../../../ENGINEERING.md)): structured, readable, explicitly named, and few in number. Test projects may use the approved packages (xUnit, FluentAssertions, FsCheck, CliWrap, Newtonsoft.Json); the shipped projects may not.

## The CLI tests

- They start the built `RepoLore.Cli.dll` with `dotnet` from a temp directory (`TestSupport.Run`), so the project must be built first and the tool is not started through `dotnet tool run`. That matters for console encoding on Windows, which a test guards.
- `Arrangements.Apply(kind, temp)` copies a fixture from `Fixtures/` (`minimal-v1`, `two-sessions`, `alpha-sparse-only`, `alpha-conflict`, `alpha-local-only`, `history-failures`) and adds the state a test needs. `Fixtures/expected-ids.tsv` is the frozen list of finding and error IDs.
- `ExecutableFoundationTests` check the build guard by building altered copies of the projects, in both the normal and the native mode.
- `Packaging/` packs the real package, restores it from a local-only feed into an empty, isolated cache, and runs a short journey against it. CI runs these tests as their own step, and on Linux a second time with networking blocked (`build/run-without-network.sh`). The general CLI step filters them out, together with the scenarios.
- `Scenarios/` replays a reference repository's history; see [Scenarios](RepoLore.Cli.Tests/Scenarios/Scenarios.md).

## Running

`dotnet build -c Release`, then `dotnet test --no-build -c Release tests/<project>`. CI runs with `restore --locked-mode`. A test that needs a real checkout works on a disposable copy: never migrate, restore or edit this repository's own knowledge to try something.

## Gotcha

Scenario case files are copied to the output folder at build time. After editing a case, rebuild instead of using `--no-build`, or the run uses the old copy.
