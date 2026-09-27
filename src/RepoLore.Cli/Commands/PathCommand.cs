using RepoLore.Core.Context;
using RepoLore.Cli.Arguments;
using RepoLore.Cli.Reading;
using RepoLore.Cli.Rendering;
using RepoLore.Cli.Repository;

namespace RepoLore.Cli.Commands;

public static class PathCommand
{
    public static int Run(CommandLine cl, string cwd)
    {
        var root = RepositoryRoot.Discover(cl.RepoRoot, cwd);

        if (cl.Target is null)
            throw new UsageException("path requires a <target>");

        var request = new ContextRequest
        {
            IncludeMethod = cl.IncludeMethod,
            SourceTarget = cl.Target
        };

        ContextSelection selection;
        try
        {
            selection = ContextSelector.Select(request);
        }
        catch (ArgumentException ex)
        {
            throw new UsageException(ex.Message);
        }

        var reader = new KnowledgeReader(root);
        var notes = new List<ResolvedNote>();
        var hasConflict = false;

        foreach (var candidate in selection.Candidates)
        {
            var resolved = reader.Read(candidate);
            if (resolved.Status == NoteStatus.Conflict)
                hasConflict = true;
            notes.Add(resolved);
        }

        Renderer.Path(notes, cl.Json);
        return hasConflict ? ExitCodes.Failure : ExitCodes.Success;
    }
}
