using RepoLore.Cli.Arguments;
using RepoLore.Cli.Rendering;
using RepoLore.Cli.Repository;
using RepoLore.Infrastructure.History;

namespace RepoLore.Cli.Commands;

public static class RestoreCommand
{
    public static int Run(CommandLine cl, string cwd)
    {
        if (cl.Session is not null || cl.Nodes.Count > 0 || cl.IncludeMethod || cl.Strict || cl.TreeStart is not null)
            throw new UsageException("restore does not take a session, --node, --include-method, --strict, or --start");

        var id = ParseRestoreId(cl.Target);

        var root = RepositoryRoot.Discover(cl.RepoRoot, cwd);
        var historyDirectory = Path.Combine(root, "_repolore", ".history");

        if (cl.DryRun)
        {
            var config = HistoryConfigLoader.Load(root);
            var plan = new RestoreEngine(root, historyDirectory).Plan(config, id, cl.RestorePath);
            Renderer.RestorePlan(plan, cl.Json, cl.Quiet);
            return ExitCodes.Success;
        }

        using var writerLock = WriterLock.Acquire(historyDirectory);
        var pending = new PendingStore(historyDirectory).Read();

        if (pending is not null)
        {
            if (pending.PreOperationId == id)
            {
                var result = new RestoreEngine(root, historyDirectory).Recover(id);
                Renderer.Restore(result, cl.Json, cl.Quiet);
                return ExitCodes.Success;
            }

            Console.Error.WriteLine($"repolore: a restore is pending; recover it with: repolore restore {pending.PreOperationId}");
            return ExitCodes.Failure;
        }

        var applyConfig = HistoryConfigLoader.Load(root);
        var applied = new RestoreEngine(root, historyDirectory).Apply(applyConfig, id, cl.RestorePath);
        Renderer.Restore(applied, cl.Json, cl.Quiet);
        return ExitCodes.Success;
    }

    private static long ParseRestoreId(string? target)
    {
        if (target is null)
            throw new UsageException("restore requires a checkpoint id (see 'history')");
        if (!long.TryParse(target, out var id) || id < 0)
            throw new UsageException("restore id must be a non-negative integer: " + target);
        return id;
    }
}
