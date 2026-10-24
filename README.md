# RepoLore

Local, offline operational memory for developers and coding agents. RepoLore keeps what a maintainer needs to know before changing an area of a repository as plain Markdown under `_repolore/`, and gives you a small `repolore` command to read it, check it and recover earlier versions of it.

> **Release candidate, no author signature.** `1.0.0-rc.1` is a release candidate for 1.0.0. The package has no author signature (NuGet.org adds only its own repository signature), so it is not an enterprise-ready or certified release. Use it where you can review and approve a pre-release.

- **Private by construction.** The tool performs no network operations, sends no telemetry, calls no AI service and starts no other programs. NuGet restore and the editors or agents you use are separate systems RepoLore does not control.
- **Plain files.** Knowledge is Markdown you edit with ordinary tools and review in ordinary Git diffs. There is no daemon, watcher, database or index.
- **Explicit sessions.** Short-term findings live in local, Gitignored `_repolore/sessions/<id>/` folders you select by name; verified findings are promoted into durable notes by a normal edit.
- **Recoverable.** `repolore checkpoint` saves complete copies of your knowledge files locally; `repolore restore` previews and applies earlier versions and preserves the current state first.

## Install

Needs a .NET 10 SDK to install and a .NET 10.0.x runtime to run. Pin the version in a tool manifest and restore it from your approved feed:

```text
dotnet new tool-manifest
dotnet tool install RepoLore.Cli --version 1.0.0-rc.1 --local
dotnet tool run repolore -- init
```

Then add `_repolore/sessions/` and `_repolore/.history/` to `.gitignore`, as `init` suggests, before creating any session.

## Use

```text
repolore context src/payments/                       # read what is known before changing a path
repolore context src/ --session <id>                 # add one explicitly chosen, provisional session
repolore checkpoint                                  # save a recoverable state
repolore restore <id> --path <note> --dry-run        # preview a restore
repolore health-check                                # structure, links, history; never fixes
```

Read the [user guide](docs/user-guide.md) for installation from an internal feed, daily use, sessions, migration from the alpha scripts, and recovery limits.

## License and security

Apache-2.0. Copyright 2026 Elias Aliche. Report suspected vulnerabilities privately, as described in [SECURITY.md](SECURITY.md).
