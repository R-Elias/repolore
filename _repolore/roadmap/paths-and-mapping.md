# Safe paths and directory mapping — package 02

One shared path resolver plus the directory-note mapping that packages 03+ reuse for reads, writes, manifests, and migration. No CLI command is added yet; the exit evidence is the test suite.

## Model

Only directories are nodes. Each directory has exactly one note, named after it and stored inside the mirrored directory. Files never get notes. The repository root maps to the fixed `root.md`. With one note per directory the mapping is a plain mirror and needs no escaping.

## Responsibilities

- `RepoLore.Core/Mapping/KnowledgePathMapper.cs` owns all mapping. Pure, no IO; mapping depends only on the path string.
  - `MapDirectoryNote(dir)` → note path under `sparse-tree/` (`src/` → `src/src.md`; root → `root.md`).
  - `TryDecode(note)` accepts only canonical directory-note paths; anything else (a file-like note, a note name that does not match its directory, or a top-level note other than `root.md`) returns a finding.
- `RepoLore.Infrastructure/RepositoryPathResolver.cs` owns physical path validation. Constructed with a canonicalized absolute root.
  - `Resolve(target)` rejects empty/`..`/`.` segments, rooted/UNC/drive paths, and symlink/reparse traversal (including an existing ancestor of a nonexistent destination). Uses a plain root-relative substring walk — a string-prefix check is not enough (`repo-other` is not `repo`).
  - `ResolveForWrite(target)` additionally detects ambiguous case/Unicode aliases on the destination filesystem and refuses over-long components before `Path.GetFullPath` runs.

## Maintenance notes

- The build guard forbids `System.Runtime.CompilerServices.Unsafe` / `MemoryMarshal`, so avoid APIs the compiler lowers to spans: `Encoding.UTF8.GetByteCount(string)`, `Path.GetRelativePath`, and multi-char `Split(char, char)` all tripped it. Length checks use a UTF-16 char-count bound (a UTF-16 char is always ≥1 UTF-8 byte). Symlink detection uses `File.GetAttributes` + `ReparsePoint` (lstat semantics).
- Mapping knowledge lives in exactly one component (Core); decode only in the same place.
- Fixture ground truth is `tests/RepoLore.Core.Tests/Fixtures/expected-mappings.tsv` (authored independently): three directory-note rows (`src/`, `src/sub/`, `a.md/`) that encode and round-trip.
