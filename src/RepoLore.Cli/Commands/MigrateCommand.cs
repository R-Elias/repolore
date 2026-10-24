using RepoLore.Cli.Arguments;
using RepoLore.Cli.Failures;
using RepoLore.Cli.Rendering;
using RepoLore.Cli.Repository;
using RepoLore.Core.Health;
using RepoLore.Core.Migration;
using RepoLore.Infrastructure.History;

namespace RepoLore.Cli.Commands;

public static class MigrateCommand
{
    public static int Run(CommandLine cl, string cwd)
    {
        var root = RepositoryRoot.Discover(cl.RepoRoot, cwd);
        var historyDirectory = Path.Combine(root, "_repolore", ".history");
        var engine = new MigrationEngine(root, historyDirectory);

        switch (engine.Classify())
        {
            case InitDirKind.Absent:
            case InitDirKind.Empty:
                return NothingToMigrate("nothing to migrate (no alpha knowledge present)", cl.Json);
            case InitDirKind.V1:
                return NothingToMigrate("_repolore/ already has a format marker; nothing to migrate", cl.Json);
        }

        var inventory = engine.Inventory();
        var mappings = MigrationPlanner.Classify(inventory.Notes);
        var destinationProblems = engine.FindDestinationProblems(mappings);

        var conflicts = inventory.SessionsNamingConflict || destinationProblems.Count > 0;
        foreach (var mapping in mappings)
            if (mapping.Status != MappingStatus.Candidate)
                conflicts = true;

        var findings = inventory.UnknownFiles.Count > 0;

        if (cl.DryRun || cl.Check)
        {
            Renderer.MigratePlan(inventory, mappings, destinationProblems, conflicts, cl.Json, cl.Quiet, cl.DryRun);
            if (conflicts)
                return ExitCodes.Failure;
            return cl.Check || findings ? ExitCodes.Findings : ExitCodes.Success;
        }

        if (conflicts)
        {
            Renderer.MigratePlan(inventory, mappings, destinationProblems, true, cl.Json, cl.Quiet, dryRun: false);
            Console.Error.WriteLine("repolore: migration blocked by unresolved conflicts; resolve them and re-run");
            return ExitCodes.Failure;
        }

        using var writerLock = WriterLock.Acquire(historyDirectory);

        var pending = new PendingStore(historyDirectory).Read();
        if (pending is not null)
        {
            throw new CliFailureException(FindingIds.HistoryPending, "an interrupted operation is pending", pending.PreOperationId);
        }

        var result = engine.Apply(inventory, mappings);
        Renderer.Migrate(result, cl.Json, cl.Quiet);
        return findings ? ExitCodes.Findings : ExitCodes.Success;
    }

    private static int NothingToMigrate(string reason, bool json)
    {
        Console.Error.WriteLine("repolore: " + reason);
        if (json)
            Renderer.MigrateNothing(reason);
        return ExitCodes.Success;
    }
}
