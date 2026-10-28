# RepoLore.Infrastructure

Every file-system effect lives here; Core decides, this applies. It still uses only the .NET base library, and the build guard applies to it as to the other shipped projects ([src](../src.md)).

## One door to the disk

`RepositoryPathResolver` resolves every target before it is read or written. It takes repository-relative, `/`-separated paths and refuses empty, `.` and `..` segments, backslashes and colons, components longer than 255 characters, anything that leaves the repository root, and any symlink or reparse point along the way. Writes additionally refuse a target whose name aliases an existing entry in its folder (a case-insensitive file system). Do not join user input onto the root by hand.

## Health checks

`Health/` combines the format check (marker, `repolore.json`, the ignore file, a leftover `_repoloreignore`), the history check, the note-structure check and the link check into one findings table. It only observes: reads, previews and health checks must never checkpoint, migrate, normalize a file or clean up history, and `health-check` reports problems without fixing them.

## History

Checkpoints, restore, init and migration are the risky part of the tool; their rules are in [History](History/History.md).
