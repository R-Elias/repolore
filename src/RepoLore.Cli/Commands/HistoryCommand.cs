using RepoLore.Cli.Arguments;
using RepoLore.Cli.Rendering;
using RepoLore.Cli.Repository;
using RepoLore.Infrastructure.History;

namespace RepoLore.Cli.Commands;

public static class HistoryCommand
{
    public static int Run(CommandLine cl, string cwd)
    {
        cl.RejectKnowledgeArguments("history");

        var root = RepositoryRoot.Discover(cl.RepoRoot, cwd);
        var historyDirectory = Path.Combine(root, "_repolore", ".history");

        var store = new CheckpointStore(historyDirectory);
        var manifests = store.ListCompleted();

        Renderer.History(manifests, cl.Json, cl.Quiet);
        return ExitCodes.Success;
    }
}
