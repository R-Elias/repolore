using RepoLore.Cli.Arguments;
using RepoLore.Cli.Rendering;
using RepoLore.Cli.Repository;
using RepoLore.Core.Migration;
using RepoLore.Infrastructure.History;

namespace RepoLore.Cli.Commands;

public static class MigrateCommand
{
    public static int Run(CommandLine cl, string cwd)
    {
        if (cl.Target is not null || cl.Session is not null || cl.Nodes.Count > 0 || cl.IncludeMethod
            || cl.Strict || cl.TreeStart is not null || cl.RestorePath is not null || cl.UpdateMethod)
            throw new UsageException("migrate does not take a target, session, --node, --include-method, --strict, --start, --path, or --update-method");

        var root = RepositoryRoot.Discover(cl.RepoRoot, cwd);
        var historyDirectory = Path.Combine(root, "_repolore", ".history");
        var engine = new MigrationEngine(root, historyDirectory);

        switch (engine.Classify())
        {
            case InitDirKind.Absent:
            case InitDirKind.Empty:
                Console.Error.WriteLine("repolore: nothing to migrate (no alpha knowledge present)");
                return ExitCodes.Success;
            case InitDirKind.V1:
                Console.Error.WriteLine("repolore: _repolore/ already has a format marker; nothing to migrate");
                return ExitCodes.Success;
        }

        var inventory = engine.Inventory();
        var mappings = MigrationPlanner.Classify(inventory.Notes);

        var conflicts = inventory.SessionsNamingConflict;
        foreach (var mapping in mappings)
            if (mapping.Status != MappingStatus.Candidate)
                conflicts = true;

        var findings = inventory.UnknownFiles.Count > 0;

        if (cl.DryRun || cl.Check)
        {
            Renderer.MigratePlan(inventory, mappings, cl.Json, cl.Quiet, cl.DryRun);
            return conflicts ? ExitCodes.Failure : (findings ? ExitCodes.Findings : ExitCodes.Success);
        }

        if (conflicts)
        {
            Renderer.MigratePlan(inventory, mappings, cl.Json, cl.Quiet, dryRun: false);
            Console.Error.WriteLine("repolore: migration blocked by unresolved conflicts; resolve them and re-run");
            return ExitCodes.Failure;
        }

        using var writerLock = WriterLock.Acquire(historyDirectory);

        var pending = new PendingStore(historyDirectory).Read();
        if (pending is not null)
        {
            Console.Error.WriteLine($"repolore: a mutation is pending; recover it with: repolore restore {pending.PreOperationId}");
            return ExitCodes.Failure;
        }

        try
        {
            var result = engine.Apply(inventory, mappings);
            Renderer.Migrate(result, cl.Json, cl.Quiet);
            return findings ? ExitCodes.Findings : ExitCodes.Success;
        }
        catch (MigrationException ex)
        {
            if (ex.RecoveryId is long id)
                Console.Error.WriteLine($"repolore: recover with: repolore restore {id}");
            Console.Error.WriteLine("repolore: " + ex.Message);
            return ExitCodes.Failure;
        }
    }
}
