using RepoLore.Cli.Arguments;
using RepoLore.Cli.Failures;
using RepoLore.Cli.Embedded;
using RepoLore.Cli.Rendering;
using RepoLore.Infrastructure.History;

namespace RepoLore.Cli.Commands;

public static class InitCommand
{
    public static int Run(CommandLine cl, string cwd)
    {
        var root = ResolveInitRoot(cl.RepoRoot, cwd);
        var historyDirectory = Path.Combine(root, "_repolore", ".history");
        var engine = new InitEngine(root, historyDirectory);

        if (engine.Classify() == InitDirKind.Alpha)
        {
            throw new CliFailureException(ErrorIds.AlphaNeedsMigration, InitEngine.MarkerlessDirectoryMessage);
        }

        using var writerLock = WriterLock.Acquire(historyDirectory);

        var result = engine.Run(EmbeddedTemplates.Method, EmbeddedTemplates.Root, cl.UpdateMethod);
        Renderer.Init(result, cl.Json, cl.Quiet);
        return ExitCodes.Success;
    }

    private static string ResolveInitRoot(string? explicitRoot, string cwd) =>
        explicitRoot is not null ? Path.GetFullPath(explicitRoot) : Path.GetFullPath(cwd);
}
