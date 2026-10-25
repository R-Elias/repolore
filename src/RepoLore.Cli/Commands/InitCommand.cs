using RepoLore.Cli.Arguments;
using RepoLore.Cli.Repository;
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

    // Without --repo-root, init must not create a knowledge base below an existing one: every other command run from
    // here would then use the nested one and hide the enclosing notes. --repo-root is the deliberate way to do that.
    private static string ResolveInitRoot(string? explicitRoot, string cwd)
    {
        if (explicitRoot is not null)
            return Path.GetFullPath(explicitRoot);

        var root = Path.GetFullPath(cwd);
        var enclosing = RepositoryRoot.FindEnclosing(root);
        if (enclosing is not null && !string.Equals(enclosing, new DirectoryInfo(root).FullName, StringComparison.Ordinal))
            throw new UsageException(
                $"a knowledge base already exists in {Path.Combine(enclosing, RepositoryRoot.RepoLoreDirectory)}, which commands run here use; " +
                "run init there, or pass --repo-root . to create a separate knowledge base in this directory");
        return root;
    }
}
