using RepoLore.Core.Configuration;
using RepoLore.Core.Health;
using RepoLore.Core.Matching;
using RepoLore.Core.Policies;
using RepoLore.Infrastructure.History;

namespace RepoLore.Infrastructure.Health;

public sealed class FormatState
{
    public FormatState(bool markerPresent, RepoLoreConfig? config, RuleSet? userIgnore)
    {
        MarkerPresent = markerPresent;
        Config = config;
        UserIgnore = userIgnore;
    }

    public bool MarkerPresent { get; }
    public RepoLoreConfig? Config { get; }
    public RuleSet? UserIgnore { get; }
}

public static class FormatHealthCheck
{
    private const string MarkerPath = "_repolore/repolore.json";

    public static FormatState Run(string repositoryRoot, string historyDirectory, List<Finding> findings)
    {
        var markerFile = Path.Combine(repositoryRoot, "_repolore", "repolore.json");
        var markerPresent = File.Exists(markerFile);
        RepoLoreConfig? config = null;

        if (markerPresent)
            config = ParseMarker(markerFile, findings);
        else if (new InitEngine(repositoryRoot, historyDirectory).Classify() == InitDirKind.Alpha)
            findings.Add(new Finding(FindingIds.MigrationNeeded, "_repolore/", "no format marker: alpha knowledge, or session folders created before 'repolore init'; see 'repolore migrate --check', or move _repolore/sessions/ aside and run 'repolore init'"));

        if (File.Exists(Path.Combine(repositoryRoot, SourceDiscoveryPolicy.LegacyIgnoreFile)))
            findings.Add(new Finding(FindingIds.PolicyError, SourceDiscoveryPolicy.LegacyIgnoreFile,
                $"the ignore file is now named {SourceDiscoveryPolicy.IgnoreFile}; rename this file, whose rules are not read until then"));

        return new FormatState(markerPresent, config, ParseIgnore(Path.Combine(repositoryRoot, SourceDiscoveryPolicy.IgnoreFile), findings));
    }

    private static RepoLoreConfig? ParseMarker(string markerFile, List<Finding> findings)
    {
        try
        {
            return ConfigParser.Parse(File.ReadAllText(markerFile));
        }
        catch (ConfigException ex)
        {
            var id = ex.Kind switch
            {
                ConfigErrorKind.UnsupportedFormat => FindingIds.UnsupportedFormat,
                ConfigErrorKind.Policy => FindingIds.PolicyError,
                _ => FindingIds.MalformedFormat
            };
            findings.Add(new Finding(id, MarkerPath, ex.Message));
            return null;
        }
    }

    private static RuleSet? ParseIgnore(string ignoreFile, List<Finding> findings)
    {
        if (!File.Exists(ignoreFile))
            return RuleSet.Empty;

        try
        {
            return RuleSet.Compile(File.ReadAllLines(ignoreFile));
        }
        catch (RuleSyntaxException ex)
        {
            findings.Add(new Finding(FindingIds.PolicyError, SourceDiscoveryPolicy.IgnoreFile, $"line {ex.Line}: {ex.Message}"));
            return null;
        }
    }
}
