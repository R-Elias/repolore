# .github/workflows

- **`ci`**: pull requests and pushes to `main`, except changes that only touch `_repolore/**`, `docs/**`, `AGENTS.md` or `method.md` (those run no CI). A new push to a pull request cancels its previous run. The `check` job runs on Windows, Linux and macOS: a locked restore, the build, the three test projects, the packaging tests (again with networking blocked on Linux) and the scenarios. The `native` job builds, tests and installs the native executable on linux-x64, win-x64 and osx-arm64.
- **`scenarios`**: Monday and Thursday at 04:17 UTC and on demand, with an optional tag filter. It mainly catches runner and SDK changes in weeks without pull requests. Scheduled runs fire only from the default branch, and GitHub disables them after 60 days without repository activity.
- **`publish`**: by hand only (`workflow_dispatch`). From a tag `v<version>` it publishes the ticked channels; from a branch it is a dry run that builds and tests everything on all six platforms and publishes nothing. Pushing a tag publishes nothing.
- **`.github/actions/native`**: one platform's native build, smoke, scenarios, packaging and install tests (pip, npm, the website script served from `127.0.0.1`). `ci` and `publish` share it.

## Gotchas

- **A run uses the workflow file of the ref it runs from.** A fix to `publish.yml` never reaches an existing tag, and a tag cannot move after anything was published from it. Test publish changes with a dry run from the branch before tagging.
- `docs/release-notes/<version>.md` must exist for the version, or the build job fails. `unreleased.md` is moved into it and reset at each release.
- Environments: `nuget-preview`, `pypi` and `npm` have the maintainer as required reviewer. `github-pages` needs a deployment rule for the tag pattern `v*`, or the website job is refused. A job naming an environment that does not exist creates it without a reviewer.
- Trusted publishing, not stored tokens: the NuGet policy's package pattern must match every ID, so a new ID needs the pattern widened first (a 403 otherwise); PyPI needs a trusted publisher for the project; npm needs one per package (owner `R-Elias`, repository `repolore`, workflow `publish.yml`, environment `npm`). Only `HOMEBREW_TAP_TOKEN` and `WINGET_TOKEN` are long-lived secrets, and both expire.
- `winget` and `homebrew` run only for stable versions, and winget opens a pull request that Microsoft reviews.
- Keep loopback-only servers in the install tests: a server on every interface waits on the macOS firewall.

Setup steps, limits and the record of each release: [distribution](../../../roadmap/distribution.md).
