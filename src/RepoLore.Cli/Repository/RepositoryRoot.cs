using RepoLore.Cli.Arguments;

namespace RepoLore.Cli.Repository;

public static class RepositoryRoot
{
    public const string RepoLoreDirectory = "_repolore";

    public static string Discover(string? explicitRoot, string cwd)
    {
        if (explicitRoot is not null)
        {
            var root = Path.GetFullPath(explicitRoot);
            if (!Directory.Exists(Path.Combine(root, RepoLoreDirectory)))
                throw new UsageException($"--repo-root does not contain a {RepoLoreDirectory}/ directory: {explicitRoot}");
            return root;
        }

        for (var current = new DirectoryInfo(cwd); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, RepoLoreDirectory)))
                return current.FullName;
        }

        throw new UsageException($"no {RepoLoreDirectory}/ directory found from the current directory or its parents");
    }
}
