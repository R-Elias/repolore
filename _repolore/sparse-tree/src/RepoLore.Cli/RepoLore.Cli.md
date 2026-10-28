# RepoLore.Cli

The packaged tool: parses arguments, runs one command, renders text or JSON, returns an exit code. It holds no rules of its own; those belong to Core and Infrastructure ([src](../src.md)).

## How a command runs

`Program.cs` parses the line (`Arguments/CommandLine`), validates the options with `CommandRules`, and dispatches to a static `<Name>Command.Run(commandLine, cwd)`. Commands find the repository with `RepositoryRoot` (walk up from the working directory to a `_repolore/` folder, or `--repo-root`), call `FormatGate.Require` so an unsupported format stops everything, and print through `Rendering/Renderer`. `--json` is detected from the raw arguments first, so even a usage error is rendered as JSON.

## Adding or changing something

- **A new option:** add it to `CommandRules.OptionsByCommand`; otherwise it is a usage error. Also update `PrintHelp` in `Program.cs`, the renderer, [the user guide](../../../../docs/user-guide.md) and a scenario.
- **A new failure:** throw `CliFailureException` with an ID from `Failures/ErrorIds`, or map a new exception type in the `catch` chain at the end of `Program.cs`. Anything unmapped becomes `internal-error`. A new ID also goes into `tests/RepoLore.Cli.Tests/Fixtures/expected-ids.tsv` and needs a scenario ([frozen contract](../src.md)).
- **Exit codes:** failures exit `3` and usage errors `2` from `Program.cs`; `1` is returned by the commands that report findings: `health-check`, `migrate --check`, and `context` when it reports a finding or, under `--strict`, omits a note. `context` exits `3` when the two copies of a note conflict.
- **Console output:** on Windows `Program.cs` switches the console to UTF-8 at start, so a native executable and `dotnet tool run` print the same bytes. A test starts the CLI directly to guard it.

## Embedded text

`Embedded/EmbeddedTemplates.cs` holds the method and the templates `init` writes. Line endings are normalized to LF in code, because a Windows checkout gives string literals CRLF and `init` must write the same bytes everywhere. Changing the method here changes what every new user gets; this checkout's two `method.md` copies then need `init --update-method` and a copy to the root file ([root files](../../../root.md#root-files)).

## Packaging

The project packs as the .NET tool `RepoLore` with the command `repolore` (the package was `RepoLore.Cli` up to 1.1.0). With `RepoLoreNative=true` it publishes as a NativeAOT executable named `repolore`; the channels, lock files and limits are in [distribution](../../../roadmap/distribution.md) and [packaging and installation](../../../roadmap/packaging-and-installation.md).
