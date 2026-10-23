using RepoLore.Core.Health;
using RepoLore.Core.Matching;
using RepoLore.Core.Policies;

namespace RepoLore.Infrastructure.Health;

public sealed class HealthChecker
{
    private readonly string _repositoryRoot;
    private readonly string _historyDirectory;
    private readonly RepositoryPathResolver _resolver;

    public HealthChecker(string repositoryRoot)
    {
        _repositoryRoot = repositoryRoot;
        _historyDirectory = Path.Combine(repositoryRoot, "_repolore", ".history");
        _resolver = new RepositoryPathResolver(repositoryRoot);
    }

    public HealthReport Check(string? sessionId, string? explainPath)
    {
        var explainTarget = explainPath is null ? null : ResolveExplainTarget(explainPath);

        var findings = new List<Finding>();
        var format = FormatHealthCheck.Run(_repositoryRoot, _historyDirectory, findings);

        CheckRequiredNotes(format.MarkerPresent, findings);
        NoteStructureCheck.Run(_repositoryRoot, _historyDirectory, findings);
        LinkCheck.CheckDurable(_repositoryRoot, findings);
        if (sessionId is not null)
            CheckSession(sessionId, findings);
        HistoryHealthCheck.Run(_historyDirectory, findings);

        findings.Sort(Compare);
        return new HealthReport(findings, explainTarget is null ? null : Explain(explainTarget, format));
    }

    private static int Compare(Finding a, Finding b)
    {
        var byPath = string.CompareOrdinal(a.Path, b.Path);
        if (byPath != 0)
            return byPath;
        var byId = string.CompareOrdinal(a.Id, b.Id);
        return byId != 0 ? byId : string.CompareOrdinal(a.Message, b.Message);
    }

    private void CheckRequiredNotes(bool markerPresent, List<Finding> findings)
    {
        RequireNote("_repolore/method.md", null, findings);
        RequireNote("_repolore/root.md", markerPresent ? null : "_repolore/tree/root.md", findings);
    }

    private void RequireNote(string path, string? alphaFallback, List<Finding> findings)
    {
        if (File.Exists(Path.Combine(_repositoryRoot, path)))
            return;
        if (alphaFallback is not null && File.Exists(Path.Combine(_repositoryRoot, alphaFallback)))
            return;
        findings.Add(new Finding(FindingIds.MissingNote, path, "required note does not exist"));
    }

    private void CheckSession(string sessionId, List<Finding> findings)
    {
        var relative = "_repolore/sessions/" + sessionId;
        string full;
        try
        {
            full = _resolver.Resolve(relative);
        }
        catch (PathResolutionException ex)
        {
            findings.Add(new Finding(FindingIds.InvalidSession, relative, ex.Message));
            return;
        }

        if (!Directory.Exists(full))
        {
            findings.Add(new Finding(FindingIds.InvalidSession, relative, $"session '{sessionId}' does not exist"));
            return;
        }

        if (!File.Exists(Path.Combine(full, "root.md")))
            findings.Add(new Finding(FindingIds.InvalidSession, relative + "/root.md", "session root.md is missing"));

        LinkCheck.CheckSession(_repositoryRoot, sessionId, full, findings);
    }

    private ExplainTarget ResolveExplainTarget(string explainPath)
    {
        var trimmed = explainPath.TrimEnd('/');
        var full = _resolver.Resolve(trimmed);
        var isDirectory = explainPath.EndsWith('/') || Directory.Exists(full);
        return new ExplainTarget(trimmed, isDirectory);
    }

    private static PolicyExplanation? Explain(ExplainTarget target, FormatState format)
    {
        if (format.UserIgnore is null || (format.MarkerPresent && format.Config is null))
            return null;

        var historyExclude = format.Config?.HistoryExclude ?? RuleSet.Empty;
        return PolicyExplanation.Explain(target.Path, target.IsDirectory, false, new SourceDiscoveryPolicy(format.UserIgnore), historyExclude);
    }

    private sealed class ExplainTarget
    {
        public ExplainTarget(string path, bool isDirectory)
        {
            Path = path;
            IsDirectory = isDirectory;
        }

        public string Path { get; }
        public bool IsDirectory { get; }
    }
}
