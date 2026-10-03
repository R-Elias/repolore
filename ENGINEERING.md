# Engineering Quality Policy

1. Segment projects into folders, and folders into subfolders, to segregate responsibility.
2. Use highly explicit names.
3. No smart code. Only dumb code is allowed.
4. Test code is code. Respect it and build it like normal code: structured, clean, very readable, with highly explicit names.
5. Use ReSharper to handle linting and first-level code smells.
6. No overengineering. Compact dumb code is good code. Do not write 500-line files for simple things.
7. Keep tests minimal. Do not create endless tests that become debt.
8. Do not write comments, except for very abstruse parts. Abstruse parts should not exist, so comments should not either.
9. Shipped code stays dependency-free: the three shipped projects (`RepoLore.Core`, `RepoLore.Infrastructure`, `RepoLore.Cli`) are BCL-only and the build guard rejects any `PackageReference`. Test projects are the one permitted exception: they may reference a small, explicitly approved test stack (xUnit, FluentAssertions, FsCheck, CliWrap, Newtonsoft.Json) to make tests cleaner and stronger. Test-only packages never weaken the shipped binary's privacy or dependency boundary.
