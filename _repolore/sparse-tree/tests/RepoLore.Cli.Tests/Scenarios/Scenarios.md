# Scenarios

Regression tests that replay a real user's history. The reference repository (`R-Elias/repolore-e2e-test-reference`) is an append-only Git history of a developer adopting RepoLore, one tagged stage after another. A scenario (`Cases/*.jsonc`) may only `checkout <tag>` and `run <repolore command>`: it never creates, edits or deletes a file, so the history is the script of edits and the tool is judged on what it does to real changes.

## The pin

`reference-repo.version` names the repository, the tag and the commit. Move it forward only, together with the scenarios that need the new stage, and push the stage first: `build/clone-reference-repo.sh` fails with a clear message when the tag is missing or has moved, so a pull request whose pin points at an unpushed stage fails its own checks. Never rewrite the reference history.

## Running

- Set `REPOLORE_REFERENCE_REPO` to a local clone that has the pinned tag; without it the scenarios are skipped. Then `dotnet test tests/RepoLore.Cli.Tests --filter "Category=Scenario"`. Build first, because the case files are copied at build time.
- By default the fixture packs the tool and installs it into an isolated cache. With `REPOLORE_SCENARIO_EXECUTABLE=<path>` it runs a native executable instead, which is how each platform's build is held to the same contract (the `native` action does this).
- `REPOLORE_SCENARIO_TAGS` narrows the run to tags (`smoke` is the quick set); each case also names the operating systems it runs on. `REPOLORE_SCENARIO_ARTIFACTS` keeps a failing scenario's files and command log. `REPOLORE_SCENARIO_WRITE_GOLDEN=1` rewrites golden outputs; review that diff like source.

## What is pinned

- Every finding and error ID in `Fixtures/expected-ids.tsv` must be asserted by at least one scenario (`ScenarioCoverageTests`).
- The cases tagged `contract`, among them `contract-frozen-for-1-0`, pin behavior that scripts rely on; changing one is a breaking change ([src](../../../src/src.md)).
- Key command outputs are compared with golden files.

CI runs the scenarios on Windows, Linux and macOS in every pull request, and the `scenarios` workflow runs them twice a week and on demand. Details: [scenario tests](../../../../roadmap/scenario-tests.md).
