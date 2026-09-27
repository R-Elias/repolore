using RepoLore.Core.Configuration;
using RepoLore.Core.Json;
using RepoLore.Core.Matching;
using RepoLore.Core.Policies;
using RepoLore.Infrastructure;

namespace RepoLore.Infrastructure.History;

public static class HistoryEnumerator
{
    public static List<string> EnumerateEligible(string repositoryRoot, RuleSet historyExclude)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
        var results = new List<string>();

        var ignorePath = Path.Combine(root, "_repoloreignore");
        if (File.Exists(ignorePath) && !RepositoryPathResolver.IsReparsePoint(ignorePath)
            && HistoryCoveragePolicy.IsCovered("_repoloreignore", false, false, historyExclude))
            results.Add("_repoloreignore");

        var repoLore = Path.Combine(root, "_repolore");
        if (Directory.Exists(repoLore))
            Walk(repoLore, "_repolore", results, historyExclude);

        results.Sort(StringComparer.Ordinal);
        return results;
    }

    private static void Walk(string directory, string relativeDirectory, List<string> results, RuleSet historyExclude)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            var name = Path.GetFileName(entry);
            var relative = relativeDirectory + "/" + name;
            var isSymlink = RepositoryPathResolver.IsReparsePoint(entry);

            if (Directory.Exists(entry))
            {
                if (isSymlink)
                    continue;
                if (relative == HistoryCoveragePolicy.HistoryDirectory)
                    continue;
                Walk(entry, relative, results, historyExclude);
                continue;
            }

            if (HistoryCoveragePolicy.IsCovered(relative, false, isSymlink, historyExclude))
                results.Add(relative);
        }
    }
}

public static class HistoryConfigLoader
{
    public static RepoLoreConfig Load(string repositoryRoot)
    {
        var path = Path.Combine(repositoryRoot, "_repolore", "repolore.json");
        if (!File.Exists(path))
            return new RepoLoreConfig(0, RepoLoreConfig.DefaultHistoryEnabled, RepoLoreConfig.DefaultMaxBytes, RuleSet.Empty, new List<string>(), new JsonObject());
        return ConfigParser.Parse(File.ReadAllText(path));
    }
}
