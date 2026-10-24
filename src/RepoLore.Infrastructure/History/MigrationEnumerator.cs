namespace RepoLore.Infrastructure.History;

public static class MigrationEnumerator
{
    private const string TreePrefix = "_repolore/tree";
    private const string SparsePrefix = "_repolore/sparse-tree";

    public static List<string> EnumerateScope(string repositoryRoot)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
        var results = new List<string>();

        var repoLore = Path.Combine(root, "_repolore");
        if (!Directory.Exists(repoLore))
            return results;

        var tree = Path.Combine(repoLore, "tree");
        if (Directory.Exists(tree))
            Walk(tree, TreePrefix, results);

        var sparse = Path.Combine(repoLore, "sparse-tree");
        if (Directory.Exists(sparse))
            Walk(sparse, SparsePrefix, results);

        AddIfFile(repoLore, "root.md", "_repolore/root.md", results);
        AddIfFile(repoLore, "repolore.json", "_repolore/repolore.json", results);

        results.Sort(StringComparer.Ordinal);
        return results;
    }

    private static void Walk(string directory, string relativeDirectory, List<string> results)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            if (RepositoryPathResolver.IsReparsePoint(entry))
                continue;

            var relative = relativeDirectory + "/" + Path.GetFileName(entry);
            if (Directory.Exists(entry))
            {
                Walk(entry, relative, results);
                continue;
            }

            results.Add(relative);
        }
    }

    private static void AddIfFile(string directory, string name, string relative, List<string> results)
    {
        var path = Path.Combine(directory, name);
        if (File.Exists(path) && !RepositoryPathResolver.IsReparsePoint(path))
            results.Add(relative);
    }
}
