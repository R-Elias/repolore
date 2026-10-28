# docs

- `user-guide.md` is the user-facing manual: install, daily use, every command and option, rule syntax, `repolore.json`, sessions, the link check, recovery from damaged history, and how to verify a release. It must describe what the code does; when behavior changes, update it in the same change.
- `release-notes/<version>.md` has one file per published version. The `publish` workflow requires the file for the version it publishes and uses it as the GitHub release text. Drafts for the next version go in `unreleased.md`, which is moved into the new file and reset at each release. A release note states the limits honestly: no author signature, no code signature on the executables.

## Gotchas

- `docs/**` is excluded from CI, so a documentation-only change runs no tests and nothing checks the guide's commands. Run what you document, by hand, before you merge.
- Each release changes the version in the install commands of the guide and of `README.md`; the README is also the package readme, and a package test pins two of its sentences ([root files](../../root.md#root-files)).
- Published release notes are history: do not edit them to match later decisions.
