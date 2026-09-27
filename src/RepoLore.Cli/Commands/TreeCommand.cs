using RepoLore.Infrastructure;
using RepoLore.Cli.Arguments;
using RepoLore.Cli.Rendering;
using RepoLore.Cli.Repository;

namespace RepoLore.Cli.Commands;

public static class TreeCommand
{
    public static int Run(CommandLine cl, string cwd)
    {
        var root = RepositoryRoot.Discover(cl.RepoRoot, cwd);

        var start = (cl.TreeStart ?? "_repolore/").TrimEnd('/');
        if (start.Length == 0)
            start = "_repolore";

        if (start != "_repolore" && !start.StartsWith("_repolore/", StringComparison.Ordinal))
            throw new UsageException("--start must be a knowledge path under _repolore/");

        if (start == "_repolore/.history" || start.StartsWith("_repolore/.history/", StringComparison.Ordinal))
            throw new UsageException("--start cannot point into history internals");

        var resolver = new RepositoryPathResolver(root);
        string fullStart;
        try
        {
            fullStart = resolver.Resolve(start);
        }
        catch (PathResolutionException ex)
        {
            throw new UsageException("invalid --start: " + ex.Message);
        }

        if (!Directory.Exists(fullStart))
            throw new UsageException("--start is not a directory: " + cl.TreeStart);

        var entries = new List<TreeEntry>();
        var sessionsRootOnly = start == "_repolore/sessions";
        Walk(fullStart, start, sessionsRootOnly, entries);

        Renderer.Tree(entries, cl.Json);
        return ExitCodes.Success;
    }

    private static void Walk(string fullDir, string relDir, bool sessionsRootOnly, List<TreeEntry> entries)
    {
        var dirs = new List<string>();
        var files = new List<string>();

        foreach (var entry in Directory.EnumerateFileSystemEntries(fullDir))
        {
            var name = Path.GetFileName(entry);
            if (relDir == "_repolore" && (name == "sessions" || name == ".history"))
                continue;

            if (Directory.Exists(entry))
                dirs.Add(entry);
            else
                files.Add(entry);
        }

        dirs.Sort(StringComparer.Ordinal);
        files.Sort(StringComparer.Ordinal);

        foreach (var dir in dirs)
            entries.Add(new TreeEntry(relDir + "/" + Path.GetFileName(dir), true));
        foreach (var file in files)
            entries.Add(new TreeEntry(relDir + "/" + Path.GetFileName(file), false));

        if (sessionsRootOnly)
            return;

        foreach (var dir in dirs)
            Walk(dir, relDir + "/" + Path.GetFileName(dir), false, entries);
    }
}
