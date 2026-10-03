using RepoLore.Core.Mapping;
using RepoLore.Infrastructure;
using FluentAssertions;
using Xunit;

namespace RepoLore.Infrastructure.Tests;

public class PathResolverTests
{
    [Fact]
    public void Resolver_rejects_escapes_rooted_unsafe_paths_and_symlink_traversal()
    {
        TempDir.WithTemp(temp =>
        {
            var root = Path.Combine(temp, "repo");
            var outside = Path.Combine(temp, "repo-other");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(outside);
            File.WriteAllText(Path.Combine(outside, "sentinel.txt"), "OUTSIDE_SENTINEL\r\n");

            var resolver = new RepositoryPathResolver(root);
            resolver.Resolve("a/b.md").Should().Be(Path.Combine(root, "a", "b.md"));

            foreach (var bad in new[] { "..", "a/../b", "a//b", "a\\b", "C:foo", "/abs/path", "." })
                new Action(() => resolver.Resolve(bad)).Should().Throw<PathResolutionException>(bad);

            var link = Path.Combine(root, "link");
            Directory.CreateSymbolicLink(link, outside);
            new Action(() => resolver.Resolve("link/sentinel.txt")).Should().Throw<PathResolutionException>("symlink ancestor");

            File.ReadAllText(Path.Combine(outside, "sentinel.txt")).Replace("\r\n", "\n").TrimEnd('\n').Should().Be("OUTSIDE_SENTINEL");
        });
    }

    [Fact]
    public void Write_alias_detection_agrees_with_the_destination_filesystem()
    {
        TempDir.WithTemp(temp =>
        {
            var dir = Path.Combine(temp, "_repolore", "sparse-tree", "src");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Foo.md"), "ALIAS_SENTINEL");
            var resolver = new RepositoryPathResolver(temp);

            var fsAliases = File.Exists(Path.Combine(dir, "foo.md"));
            var refused = false;
            try { resolver.ResolveForWrite("_repolore/sparse-tree/src/foo.md"); }
            catch (PathResolutionException) { refused = true; }
            refused.Should().Be(fsAliases, "resolver must agree with filesystem alias behavior");

            resolver.ResolveForWrite("_repolore/sparse-tree/src/bar.md").Should().Be(Path.Combine(dir, "bar.md"));
        });
    }

    [Fact]
    public void Over_long_mapped_names_are_refused_not_truncated()
    {
        TempDir.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore", "sparse-tree"));
            var resolver = new RepositoryPathResolver(temp);

            var longName = new string('a', 253);
            var note = KnowledgePathMapper.MapDirectoryNote(longName + "/");
            new Action(() => resolver.ResolveForWrite("_repolore/sparse-tree/" + note)).Should().Throw<PathResolutionException>("over-long note name");

            resolver.ResolveForWrite("_repolore/sparse-tree/ok.md").Should().Be(Path.Combine(temp, "_repolore", "sparse-tree", "ok.md"));
        });
    }
}
