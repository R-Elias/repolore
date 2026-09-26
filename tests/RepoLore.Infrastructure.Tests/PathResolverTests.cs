using RepoLore.Core.Mapping;
using RepoLore.Infrastructure;

namespace RepoLore.Infrastructure.Tests;

public static class PathResolverTests
{
    public static void Run()
    {
        TestRunner.Check("resolver rejects escapes, rooted/unsafe paths, and symlink traversal", () =>
        {
            WithTemp(temp =>
            {
                var root = Path.Combine(temp, "repo");
                var outside = Path.Combine(temp, "repo-other");
                Directory.CreateDirectory(root);
                Directory.CreateDirectory(outside);
                File.WriteAllText(Path.Combine(outside, "sentinel.txt"), "OUTSIDE_SENTINEL\r\n");

                var resolver = new RepositoryPathResolver(root);
                TestRunner.Equal(Path.Combine(root, "a", "b.md"), resolver.Resolve("a/b.md"));

                foreach (var bad in new[] { "..", "a/../b", "a//b", "a\\b", "C:foo", "/abs/path", "." })
                    TestRunner.Throws<PathResolutionException>(() => resolver.Resolve(bad), bad);

                var link = Path.Combine(root, "link");
                Directory.CreateSymbolicLink(link, outside);
                TestRunner.Throws<PathResolutionException>(() => resolver.Resolve("link/sentinel.txt"), "symlink ancestor");

                TestRunner.Equal("OUTSIDE_SENTINEL", File.ReadAllText(Path.Combine(outside, "sentinel.txt")).Replace("\r\n", "\n").TrimEnd('\n'));
            });
        });

        TestRunner.Check("write alias detection agrees with the destination filesystem", () =>
        {
            WithTemp(temp =>
            {
                var dir = Path.Combine(temp, "_repolore", "sparse-tree", "src");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "Foo.md"), "ALIAS_SENTINEL");
                var resolver = new RepositoryPathResolver(temp);

                var fsAliases = File.Exists(Path.Combine(dir, "foo.md"));
                var refused = false;
                try { resolver.ResolveForWrite("_repolore/sparse-tree/src/foo.md"); }
                catch (PathResolutionException) { refused = true; }
                TestRunner.Equal(fsAliases, refused, "resolver must agree with filesystem alias behavior");

                TestRunner.Equal(Path.Combine(dir, "bar.md"), resolver.ResolveForWrite("_repolore/sparse-tree/src/bar.md"));
            });
        });

        TestRunner.Check("over-long mapped names are refused, not truncated", () =>
        {
            WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore", "sparse-tree"));
                var resolver = new RepositoryPathResolver(temp);

                var longName = new string('a', 253);
                var note = KnowledgePathMapper.MapDirectoryNote(longName + "/");
                TestRunner.Throws<PathResolutionException>(() => resolver.ResolveForWrite("_repolore/sparse-tree/" + note), "over-long note name");

                TestRunner.Equal(Path.Combine(temp, "_repolore", "sparse-tree", "ok.md"),
                      resolver.ResolveForWrite("_repolore/sparse-tree/ok.md"));
            });
        });
    }

    private static void WithTemp(Action<string> action)
    {
        var temp = Path.Combine(Path.GetTempPath(), "repolore-infra-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try { action(temp); }
        finally { Directory.Delete(temp, recursive: true); }
    }
}
