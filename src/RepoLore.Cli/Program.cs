using RepoLore.Cli;
using RepoLore.Cli.Arguments;
using RepoLore.Cli.Commands;
using RepoLore.Core.Configuration;
using RepoLore.Core.Format;
using RepoLore.Core.Restore;
using RepoLore.Infrastructure.History;

try
{
    var cl = CommandLine.Parse(args);

    if (cl.Help)
    {
        PrintHelp();
        return ExitCodes.Success;
    }

    switch (cl.Command)
    {
        case "version":
            if (cl.Target is not null || cl.SawOption)
                return Usage("version takes no arguments");
            Console.WriteLine($"RepoLore {CliVersion.Value}");
            Console.WriteLine($"Supported knowledge formats: {KnowledgeFormat.Current}");
            return ExitCodes.Success;

        case "path":
            return PathCommand.Run(cl, Environment.CurrentDirectory);

        case "context":
            return ContextCommand.Run(cl, Environment.CurrentDirectory);

        case "tree":
            return TreeCommand.Run(cl, Environment.CurrentDirectory);

        case "checkpoint":
            return CheckpointCommand.Run(cl, Environment.CurrentDirectory);

        case "history":
            return HistoryCommand.Run(cl, Environment.CurrentDirectory);

        case "restore":
            return RestoreCommand.Run(cl, Environment.CurrentDirectory);

        case "init":
            return InitCommand.Run(cl, Environment.CurrentDirectory);

        case null:
            return Usage("missing command");

        default:
            return Usage("unknown command: " + cl.Command);
    }
}
catch (UsageException ex)
{
    return Usage(ex.Message);
}
catch (ArgumentException ex)
{
    return Usage(ex.Message);
}
catch (ConfigException ex)
{
    return OperationalError(ex.Message);
}
catch (WriterLockException ex)
{
    return OperationalError(ex.Message);
}
catch (HistoryStoreException ex)
{
    return OperationalError(ex.Message);
}
catch (RestoreException ex)
{
    Console.Error.WriteLine($"repolore: {ex.Message}");
    Console.Error.WriteLine($"repolore: recover with: repolore restore {ex.RecoveryId}");
    return ExitCodes.Failure;
}
catch (RestorePlanException ex)
{
    return OperationalError(ex.Message);
}

static int Usage(string message)
{
    Console.Error.WriteLine("Usage: repolore version | --help | init [--update-method] | path <target> | context [<target>] | tree | checkpoint | history | restore <id>");
    Console.Error.WriteLine(message);
    return ExitCodes.Usage;
}

static int OperationalError(string message)
{
    Console.Error.WriteLine("repolore: " + message);
    return ExitCodes.Failure;
}

static void PrintHelp()
{
    Console.WriteLine("Usage: repolore <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  version                                    Print CLI and knowledge-format versions.");
    Console.WriteLine("  path <target> [--include-method]           List durable notes for a source path with status.");
    Console.WriteLine("  context [<target>] [--session <id>]        Read selected durable and session notes.");
    Console.WriteLine("            [--node <path>]... [--budget-tokens <n>] [--strict] [--include-method]");
    Console.WriteLine("  tree [--start <path>]                      List the durable knowledge tree.");
    Console.WriteLine("  checkpoint                                 Capture the current eligible state.");
    Console.WriteLine("  history                                    List completed local recovery checkpoints.");
    Console.WriteLine("  restore <id> [--path <path>] [--dry-run]   Preview or restore a checkpoint within coverage.");
    Console.WriteLine("  init [--update-method]                     Create the missing knowledge base and first checkpoint.");
    Console.WriteLine();
    Console.WriteLine("Common options: --repo-root <path>, --json, --quiet, --help");
}
