# Configuration and coverage policies — package 03

Format/config parsing and one small matcher, plus the two independent coverage policies. No CLI command is added yet; the exit evidence is the Core test suite. All components are pure (no IO) and live under `RepoLore.Core`, split into responsibility folders that map one-to-one to namespaces:

```text
src/RepoLore.Core/
  Format/          KnowledgeFormat.cs            (RepoLore.Core.Format)
  Json/            Json.cs                       (RepoLore.Core.Json)
  Mapping/         KnowledgePathMapper.cs        (RepoLore.Core.Mapping)
  Matching/        RuleSet.cs                    (RepoLore.Core.Matching)
  Configuration/   RepoLoreConfig.cs, ConfigParser.cs
  Policies/        SourceDiscoveryPolicy.cs, HistoryCoveragePolicy.cs
```

Dependencies are acyclic: `Configuration` uses `Json`/`Matching`/`Format`, `Policies` uses `Matching`; the leaf folders use none. Tests mirror the folders under `tests/RepoLore.Core.Tests/{Mapping,Matching,Policies,Configuration}/`.

## Model

- `Json/Json.cs` — a small hand-written JSON parser (`JsonParser`) and writer (`JsonWriter`) over an explicit `JsonValue` tree. It exists to reject duplicate keys and malformed input deterministically and to retain unknown fields for later rewrites; `System.Text.Json` silently last-wins on duplicates, so it is not used here.
- `Configuration/ConfigParser.cs` (and `RepoLoreConfig.cs`) — parses `_repolore/repolore.json` into a `RepoLoreConfig`. `formatVersion` is required and must equal `KnowledgeFormat.Current`; higher versions fail with an upgrade instruction. `history` is optional; `enabled` (default `true`) and `maxBytes` (default `209715200`) are optional, `exclude` defaults to empty. Negative/non-integral/overflowing `maxBytes`, duplicate keys, wrong types, and invalid `history.exclude` rules are rejected before any mutation. Malformed JSON is an error, never an alpha fallback.
- `Matching/RuleSet.cs` — the one bounded matcher for `_repoloreignore` and `history.exclude`. Grammar: root-relative rules, `/` separators, ordinal case-sensitive matching, optional leading `/` (cosmetic), `*`/`?` within a segment, `**` only as a whole segment, a trailing `/` matches directories and their descendants (not a same-named file), leading `!` includes, `#`/blank lines are ignored, `\#`/`\!` escape literal leading characters. Last matching rule wins. Unsupported syntax (`[...]`, stray `\`, embedded `**`, empty segments) fails with a line number.

## Policies

- `Policies/SourceDiscoveryPolicy.cs` — source discovery is the layered exclusion: hard safety exclusions (`_repolore/`, `.git/`, `.hg/`, `.svn/`) → configurable defaults (`**/bin/`, `**/obj/`, `**/node_modules/`, `**/build/`, `**/vendor/`) → user `_repoloreignore` overrides. Hard exclusions can never be re-included. `HasNegation` tells a future walker to traverse user-excluded directories so re-inclusion works. No Git invocation.
- `Policies/HistoryCoveragePolicy.cs` — separate eligibility decision over regular files: root `_repoloreignore`, `_repolore/repolore.json`, and all `.md` under `_repolore/` (path notes, custom areas, all sessions). Always excluded: directories, symlinks, and the `.history/` subtree (recovery state plus its `tmp/` staging). `history.exclude` uses the same matcher grammar and only affects this coverage; source-discovery rules never apply. Selecting one session for context never limits coverage — coverage is a pure function of the path.

## Maintenance notes

- These policies are pure decision functions; no policy change deletes a file (mutations live in packages 05–07). "Missing marker is alpha (format 0)" is a filesystem-presence rule wired by init/migration (packages 08–09), not by the text parser.
- Frozen fixtures: `tests/RepoLore.Core.Tests/Fixtures/ignore-rules.txt` (authored rule set) and `expected-matcher.tsv` (path, is-dir, decision), authored independently of the matcher.
