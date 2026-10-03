using RepoLore.Cli.Arguments;
using RepoLore.Cli.Embedded;
using RepoLore.Cli.Rendering;
using RepoLore.Infrastructure.History;

namespace RepoLore.Cli.Commands;

public static class InitCommand
{
    public static int Run(CommandLine cl, string cwd)
    {
        if (cl.Target is not null || cl.Session is not null || cl.Nodes.Count > 0 || cl.IncludeMethod
            || cl.Strict || cl.TreeStart is not null || cl.RestorePath is not null || cl.DryRun)
            throw new UsageException("init does not take a target, session, --node, --include-method, --strict, --start, --path, or --dry-run");

        var root = ResolveInitRoot(cl.RepoRoot, cwd);
        var historyDirectory = Path.Combine(root, "_repolore", ".history");
        var engine = new InitEngine(root, historyDirectory);

        if (engine.Classify() == InitDirKind.Alpha)
        {
            Console.Error.WriteLine("repolore: _repolore/ contains alpha knowledge without a format marker; run 'repolore migrate' to migrate it");
            return ExitCodes.Failure;
        }

        using var writerLock = WriterLock.Acquire(historyDirectory);

        try
        {
            var result = engine.Run(EmbeddedTemplates.Method, EmbeddedTemplates.Root, cl.UpdateMethod);
            Renderer.Init(result, cl.Json, cl.Quiet);
            return ExitCodes.Success;
        }
        catch (InitIncompleteException ex)
        {
            Console.Error.WriteLine("repolore: " + ex.Message);
            foreach (var path in ex.Created)
                Console.Error.WriteLine("repolore:   created " + path);
            Console.Error.WriteLine("repolore: rerun 'repolore init' to finish; nothing was deleted");
            return ExitCodes.Failure;
        }
        catch (InitException ex)
        {
            if (ex.RecoveryId is long id)
                Console.Error.WriteLine($"repolore: recover with: repolore restore {id}");
            Console.Error.WriteLine("repolore: " + ex.Message);
            return ExitCodes.Failure;
        }
    }

    private static string ResolveInitRoot(string? explicitRoot, string cwd) =>
        explicitRoot is not null ? Path.GetFullPath(explicitRoot) : Path.GetFullPath(cwd);
}
