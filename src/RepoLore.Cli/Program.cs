using RepoLore.Cli;
using RepoLore.Cli.Arguments;
using RepoLore.Cli.Commands;
using RepoLore.Cli.Failures;
using RepoLore.Cli.Rendering;
using RepoLore.Core.Configuration;
using RepoLore.Core.Format;
using RepoLore.Core.Health;
using RepoLore.Core.Json;
using RepoLore.Core.Restore;
using RepoLore.Infrastructure;
using RepoLore.Infrastructure.History;

// Agents read the output through a pipe, so it is UTF-8 everywhere: on Windows the console's code page would otherwise
// apply, losing characters it lacks and differing between channels (the .NET CLI switches the console to UTF-8 before
// it runs a tool; a native executable starts directly). Without a console there is nothing to switch.
if (OperatingSystem.IsWindows())
{
    try { Console.OutputEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false); }
    catch (IOException) { }
}

var json = Array.IndexOf(args, "--json") >= 0;

try
{
    var cl = CommandLine.Parse(args);

    if (cl.Help)
    {
        PrintHelp();
        return ExitCodes.Success;
    }

    CommandRules.Validate(cl);

    switch (cl.Command)
    {
        case "version":
            PrintVersion(cl.Json);
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

        case "migrate":
            return MigrateCommand.Run(cl, Environment.CurrentDirectory);

        case "health-check":
            return HealthCheckCommand.Run(cl, Environment.CurrentDirectory);

        case null:
            throw new UsageException("missing command");

        default:
            throw new UsageException("unknown command: " + cl.Command);
    }
}
catch (UsageException ex)
{
    return Usage(json, ex.Message);
}
catch (ArgumentException ex)
{
    return Usage(json, ex.Message);
}
catch (CliFailureException ex)
{
    return Failure(json, ex.Id, ex.Message, ex.RecoveryId);
}
catch (ConfigException ex)
{
    return Failure(json, ConfigFailureId(ex.Kind), ex.Message, null);
}
catch (WriterLockException ex)
{
    return Failure(json, ErrorIds.HistoryLocked, ex.Message, null);
}
catch (UnknownCheckpointException ex)
{
    return Failure(json, ErrorIds.UnknownCheckpoint, ex.Message, null);
}
catch (HistoryStoreException ex)
{
    return Failure(json, ErrorIds.HistoryFailure, ex.Message, null);
}
catch (RestoreException ex)
{
    return Failure(json, ErrorIds.OperationInterrupted, ex.Message, ex.RecoveryId);
}
catch (RestorePlanException ex)
{
    return Failure(json, ErrorIds.RestorePlanInvalid, ex.Message, null);
}
catch (InitIncompleteException ex)
{
    return Failure(json, ErrorIds.InitIncomplete,
        ex.Message + "; already created: " + string.Join(", ", ex.Created) + "; rerun 'repolore init' to finish, nothing was deleted", null);
}
catch (InitException ex)
{
    return Failure(json, ex.RecoveryId is null ? ErrorIds.HistoryFailure : ErrorIds.OperationInterrupted, ex.Message, ex.RecoveryId);
}
catch (MigrationException ex)
{
    return Failure(json, ex.RecoveryId is null ? ErrorIds.MigrationBlocked : ErrorIds.OperationInterrupted, ex.Message, ex.RecoveryId);
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PathResolutionException)
{
    return Failure(json, ErrorIds.IoError, ex.Message, null);
}
catch (Exception ex)
{
    return Failure(json, ErrorIds.InternalError, ex.Message, null);
}

static int Usage(bool json, string message)
{
    Console.Error.WriteLine("Usage: repolore <command> [options]; run 'repolore --help' for the commands and options.");
    ErrorRenderer.Write(json, ErrorIds.Usage, message, null);
    return ExitCodes.Usage;
}

static int Failure(bool json, string id, string message, long? recoveryId)
{
    ErrorRenderer.Write(json, id, message, recoveryId);
    return ExitCodes.Failure;
}

static string ConfigFailureId(ConfigErrorKind kind) => kind switch
{
    ConfigErrorKind.UnsupportedFormat => FindingIds.UnsupportedFormat,
    ConfigErrorKind.Policy => FindingIds.PolicyError,
    _ => FindingIds.MalformedFormat
};

static void PrintVersion(bool json)
{
    if (!json)
    {
        Console.WriteLine($"RepoLore {CliVersion.Value}");
        Console.WriteLine($"Supported knowledge formats: {KnowledgeFormat.Current}");
        return;
    }

    var root = new JsonObject();
    root.Members.Add(new JsonMember("version", new JsonStringValue(CliVersion.Value)));
    var formats = new JsonArray();
    formats.Items.Add(Renderer.Number(KnowledgeFormat.Current));
    root.Members.Add(new JsonMember("supportedKnowledgeFormats", formats));
    Console.WriteLine(JsonWriter.Write(root));
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
    Console.WriteLine("  migrate [--check | --dry-run]              Migrate alpha knowledge to v1 (check, preview, or apply).");
    Console.WriteLine("  health-check [--explain <path>] [--session <id>]");
    Console.WriteLine("                                             Report structural findings; observes only, never fixes.");
    Console.WriteLine();
    Console.WriteLine("Common options: --repo-root <path>, --json, --quiet, --help");
}
