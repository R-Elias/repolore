# Packaged tool and restricted installation — package 11

The channels other than NuGet (native executables, PyPI, npm, Homebrew, winget, the website) are in [distribution](distribution.md).

`RepoLore.Cli` ships as one framework-dependent .NET tool package. See the
[implementation guide](implementation-v1.md#11--verify-the-package-users-will-actually-install) for the gates and
[roadmap section 8](roadmap.md) for the release contract. Nothing has been published; no placeholder was pushed to reserve the ID.

## Identity and version sources

| Fact | Single source |
|---|---|
| Package version (also the `version` command output) | `<Version>` in `Directory.Build.props` |
| Package ID, command name, license, authors, description, URLs | `src/RepoLore.Cli/RepoLore.Cli.csproj` (`PackageId`, `ToolCommandName`, …) |
| Runtime roll-forward | `<RollForward>LatestPatch</RollForward>` in the same csproj |

Changing the ID if `RepoLore.Cli` turns out to be unavailable means editing `PackageId` once; the package tests read id and version from the produced `.nupkg` and compare the version with `Directory.Build.props`, so no test holds a copy of the version. The tests do assert the public contract literals `RepoLore.Cli` and `repolore`, so an ID change updates those two assertions deliberately.

`DeterministicSourcePaths=true` (`Directory.Build.props`) rewrites build paths to `/_/…`. Without it the shipped `RepoLore.Cli.dll` embedded the developer's absolute checkout path via the pdb path; `Package_files_contain_no_build_machine_paths` fails when it is removed.

## What the package contains

Exactly (asserted as a whitelist, so any new file is a deliberate review): `tools/net10.0/any/` with `DotnetToolSettings.xml` (command `repolore`), `RepoLore.Cli.{dll,pdb,deps.json,runtimeconfig.json}`, `RepoLore.Core.{dll,pdb}`, `RepoLore.Infrastructure.{dll,pdb}`, the root `README.md`, plus the nuspec/OPC metadata. No NuGet dependencies, no test-stack assemblies, no knowledge, sessions, history, fixtures, or configuration files. Packing `Core` and `Infrastructure` produces nothing (`IsPackable=false`), so the feed holds one package. The package readme is the repository `README.md` (`PackageReadmeFile`, added in package 12 with the whitelist change; `Package_readme_is_the_repository_readme_and_states_the_preview_signature_limitation` pins it). The user-facing install and usage instructions are `docs/user-guide.md`; see [dogfood and preview](dogfood-and-preview.md).

## Supported environments

- Install needs a .NET **10** SDK; running needs a .NET **10.0.x** runtime. `LatestPatch` rolls forward only within 10.0; a .NET 11 runtime is not used.
- Under a .NET 9 SDK `dotnet tool restore` refuses the package with the SDK's own `Settings file 'DotnetToolSettings.xml' was not found in the package` exception (observed manually; the package only has a `net10.0` folder). That is expected, not a RepoLore defect; do not add an untested `net9.0` build to hide it.
- The tool is language-agnostic: the smoke repository contains no .NET project.
- NuGet installs neither the SDK nor the runtime.

## Recommended installation (pinned local manifest)

Commit `.config/dotnet-tools.json`:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "repolore.cli": { "version": "<exact version>", "commands": [ "repolore" ], "rollForward": false }
  }
}
```

and an approved `NuGet.Config` that clears other sources:

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="approved-feed" value="https://feed.example.invalid/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

Then `dotnet tool restore` and `dotnet tool run repolore -- <command>` (or `dotnet repolore`). Commit no credentials. `dotnet tool install RepoLore.Cli --version <v> --local` after `dotnet new tool-manifest` writes the same manifest (verified manually). Global installation (`dotnet tool install --global`) is an individual-user alternative and is **not** exercised by the package tests. This checkout deliberately has no tool manifest: the tool is unpublished and the checkout was migrated by running the packed tool from a manifest kept outside it ([dogfood and preview](dogfood-and-preview.md)); a committed manifest needs a published version to pin.

## Checks

`tests/RepoLore.Cli.Tests/Packaging/` (run in CI as its own step, excluded from the general Cli step by a `FullyQualifiedName` filter):

- `PackedToolFixture` packs the three shipped projects (`--no-restore`, Release) into a fresh temp feed once per run and exposes the archive.
- `PackageContentsTests` inspects the real archive: single package, version source, tool type and no dependencies, metadata not the SDK placeholder, entry whitelist, no knowledge/test-stack names, no build-machine paths, `.deps.json` lists only the three project libraries, runtimeconfig is net10 with `LatestPatch`.
- `IsolatedToolEnvironment` gives each test its own `NuGet.config` (`<clear/>` plus the one local feed, passed with `--configfile`), `NUGET_PACKAGES`, HTTP/plugin caches and `DOTNET_CLI_HOME`, and a consumer repository outside the checkout.
- `InstalledToolTests`: restore a pinned manifest into an empty cache and run `version`, `init`, `context`, `checkpoint` ×2, `history`, `restore`, `health-check` on a temp repository, asserting the assembly used is the restored copy. Negative controls: an empty feed with an empty cache must fail for `RepoLore.Cli` and for a package known to exist on nuget.org (`dotnetsay` 2.1.7), proving no cached or public fallback. A warm cache would satisfy a restore regardless of the feed (NuGet behaviour), which is why every test uses an empty one.

`build/run-without-network.sh` (Linux, root) re-executes a command inside `unshare --net` with only loopback and refuses to continue if outbound TCP to a public address or DNS for `api.nuget.org` still works. CI runs the Packaging tests through it on Linux. Everything the step needs (SDK, built binaries, the pack inputs) already exists because it follows restore/build; the tests themselves need no network.

## Publication

`RepoLore.Cli` `0.1.0-preview.1` was published on 2026-10-24. The publish path is the `publish.yml` Trusted Publishing workflow plus `build/smoke-exact-package.sh`; see [dogfood and preview](dogfood-and-preview.md). The Packaging tests above pack their own copy; the smoke script is what exercises the exact file that is pushed.

## Not covered

- No network-call trace of the smoke path exists and none is planned (maintainer decision of 2026-10-24, see [v1.0.0 release plan](release-v1.md)); static guards plus the blocked-network run are the evidence. One disconnected success does not prove zero networking.
- SBOM, checksums and tag/version verification are package 13. Author signing is deferred for v1.0.0 by the maintainer's decision of 2026-10-24 ([v1.0.0 release plan](release-v1.md)); the package carries only NuGet.org's repository signature.
- NuGet.org ID ownership and any `RepoLore.` prefix reservation cannot be confirmed without an account; a read-only lookup only shows nothing is published.
