using RepoLore.Cli.Arguments;
using RepoLore.Cli.Rendering;
using RepoLore.Cli.Repository;
using RepoLore.Infrastructure.History;

namespace RepoLore.Cli.Commands;

public static class CheckpointCommand
{
    public static int Run(CommandLine cl, string cwd)
    {
        cl.RejectKnowledgeArguments("checkpoint");

        var root = RepositoryRoot.Discover(cl.RepoRoot, cwd);
        var historyDirectory = Path.Combine(root, "_repolore", ".history");

        var config = HistoryConfigLoader.Load(root);
        if (!config.HistoryEnabled)
        {
            Console.Error.WriteLine("repolore: history is disabled in _repolore/repolore.json; enable it before checkpointing");
            return ExitCodes.Failure;
        }

        using var writerLock = WriterLock.Acquire(historyDirectory);

        var pending = new PendingStore(historyDirectory).Read();
        if (pending is not null)
        {
            Console.Error.WriteLine($"repolore: a restore is pending; recover it with: repolore restore {pending.PreOperationId}");
            return ExitCodes.Failure;
        }

        var engine = new CheckpointEngine(root, historyDirectory);
        var result = engine.Capture(config);

        Renderer.Checkpoint(result, cl.Json, cl.Quiet);
        return ExitCodes.Success;
    }
}
