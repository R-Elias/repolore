using RepoLore.Cli.Arguments;
using RepoLore.Cli.Failures;
using RepoLore.Cli.Rendering;
using RepoLore.Core.Health;
using RepoLore.Cli.Repository;
using RepoLore.Infrastructure.History;

namespace RepoLore.Cli.Commands;

public static class CheckpointCommand
{
    public static int Run(CommandLine cl, string cwd)
    {
        var root = RepositoryRoot.Discover(cl.RepoRoot, cwd);
        var historyDirectory = Path.Combine(root, "_repolore", ".history");

        var config = HistoryConfigLoader.Load(root);
        if (!config.HistoryEnabled)
        {
            throw new CliFailureException(ErrorIds.HistoryDisabled, "history is disabled in _repolore/repolore.json; enable it before checkpointing");
        }

        using var writerLock = WriterLock.Acquire(historyDirectory);

        var pending = new PendingStore(historyDirectory).Read();
        if (pending is not null)
        {
            throw new CliFailureException(FindingIds.HistoryPending, "an interrupted operation is pending", pending.PreOperationId);
        }

        var engine = new CheckpointEngine(root, historyDirectory);
        var result = engine.Capture(config);

        Renderer.Checkpoint(result, cl.Json, cl.Quiet);
        return ExitCodes.Success;
    }
}
