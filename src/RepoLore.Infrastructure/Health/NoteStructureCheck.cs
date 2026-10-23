using RepoLore.Core.Health;
using RepoLore.Core.Migration;
using RepoLore.Infrastructure.History;

namespace RepoLore.Infrastructure.Health;

public static class NoteStructureCheck
{
    private const string RootKey = "root";

    public static void Run(string repositoryRoot, string historyDirectory, List<Finding> findings)
    {
        var inventory = new MigrationEngine(repositoryRoot, historyDirectory).Inventory();
        var resolver = new RepositoryPathResolver(repositoryRoot);

        foreach (var mapping in MigrationPlanner.Classify(inventory.Notes))
        {
            switch (mapping.Status)
            {
                case MappingStatus.Conflict:
                    findings.Add(new Finding(FindingIds.ConflictingAlphaNotes, mapping.Destination,
                        mapping.Finding + "; run 'repolore checkpoint' to preserve both variants, then resolve by hand"));
                    break;
                case MappingStatus.Ambiguous:
                    findings.Add(new Finding(FindingIds.InvalidNoteName, mapping.Key, mapping.Finding ?? "note name does not match its directory"));
                    break;
                default:
                    if (mapping.NormalizedText is not null && mapping.Key != RootKey && !SourceDirectoryExists(resolver, mapping.Key))
                        findings.Add(new Finding(FindingIds.OrphanPathNote, mapping.Destination, $"source directory '{mapping.Key}' does not exist"));
                    break;
            }
        }
    }

    private static bool SourceDirectoryExists(RepositoryPathResolver resolver, string key)
    {
        try
        {
            return Directory.Exists(resolver.Resolve(key.TrimEnd('/')));
        }
        catch (PathResolutionException)
        {
            return true;
        }
    }
}
