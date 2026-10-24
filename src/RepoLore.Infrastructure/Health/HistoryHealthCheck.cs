using RepoLore.Core.Health;
using RepoLore.Core.Snapshot;
using RepoLore.Infrastructure.History;

namespace RepoLore.Infrastructure.Health;

public static class HistoryHealthCheck
{
    private const string HistoryPath = "_repolore/.history";

    public static void Run(string historyDirectory, List<Finding> findings)
    {
        if (File.Exists(historyDirectory))
        {
            findings.Add(new Finding(FindingIds.HistoryUnavailable, HistoryPath, "history path exists but is not a directory"));
            return;
        }
        if (!Directory.Exists(historyDirectory))
            return;
        if (RepositoryPathResolver.IsReparsePoint(historyDirectory))
        {
            findings.Add(new Finding(FindingIds.HistoryUnavailable, HistoryPath, "history directory is a symlink"));
            return;
        }

        try
        {
            CheckPending(historyDirectory, findings);
            CheckCheckpointsAndObjects(historyDirectory, findings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            findings.Add(new Finding(FindingIds.HistoryUnavailable, HistoryPath, ex.Message));
        }
    }

    private static void CheckPending(string historyDirectory, List<Finding> findings)
    {
        try
        {
            var pending = new PendingStore(historyDirectory).Read();
            if (pending is not null)
                findings.Add(new Finding(FindingIds.HistoryPending, HistoryPath + "/pending.json",
                    $"an interrupted operation is pending; recover it with 'repolore restore {pending.PreOperationId}'"));
        }
        catch (HistoryStoreException ex)
        {
            findings.Add(new Finding(FindingIds.HistoryCorrupt, HistoryPath + "/pending.json", ex.Message));
        }
    }

    private static void CheckCheckpointsAndObjects(string historyDirectory, List<Finding> findings)
    {
        List<CheckpointManifest> manifests;
        try
        {
            manifests = new CheckpointStore(historyDirectory).ListCompleted();
        }
        catch (HistoryStoreException ex)
        {
            findings.Add(new Finding(FindingIds.HistoryCorrupt, HistoryPath + "/checkpoints", ex.Message));
            return;
        }

        var firstReference = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var manifest in manifests)
            foreach (var file in manifest.Files)
                firstReference.TryAdd(file.Hash, manifest.Id);

        var objects = new ObjectStore(historyDirectory);
        foreach (var reference in firstReference)
            if (!objects.Validate(reference.Key))
                findings.Add(new Finding(FindingIds.HistoryCorrupt, HistoryPath + "/objects/" + reference.Key,
                    $"object is missing or corrupt (referenced by checkpoint {reference.Value})"));
    }
}
