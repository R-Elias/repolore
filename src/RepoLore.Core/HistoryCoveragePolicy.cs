namespace RepoLore.Core;

public static class HistoryCoveragePolicy
{
    public const string HistoryDirectory = "_repolore/.history";

    public static bool IsEligible(string relativePath, bool isDirectory, bool isSymlink)
    {
        if (isDirectory)
            return false;
        if (isSymlink)
            return false;
        if (relativePath == "_repoloreignore")
            return true;
        if (relativePath == "_repolore/repolore.json")
            return true;
        if (IsUnderHistory(relativePath))
            return false;
        return relativePath.StartsWith("_repolore/", StringComparison.Ordinal)
            && relativePath.EndsWith(".md", StringComparison.Ordinal);
    }

    public static bool IsCovered(string relativePath, bool isDirectory, bool isSymlink, RuleSet historyExclude)
    {
        if (!IsEligible(relativePath, isDirectory, isSymlink))
            return false;
        return !historyExclude.IsExcluded(relativePath, isDirectory);
    }

    private static bool IsUnderHistory(string relativePath) =>
        relativePath == HistoryDirectory
        || relativePath.StartsWith(HistoryDirectory + "/", StringComparison.Ordinal);
}
