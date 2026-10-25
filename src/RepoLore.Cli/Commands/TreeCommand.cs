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
        FormatGate.Require(root);

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

        var entries = start == "_repolore/sessions" ? ListSessions(fullStart, start) : Walk(fullStart, start);

        Renderer.Tree(entries, cl.Json, cl.Quiet);
        return ExitCodes.Success;
    }

    // Lists only what the other commands treat as knowledge: Markdown notes and the directories that lead to one.
    // Symlinks, other files and directories without a note are left out. Each directory's notes come first, then each
    // subdirectory followed by its contents, all in ordinal order.
    private static List<TreeEntry> Walk(string fullDir, string relDir)
    {
        var dirs = new List<string>();
        var notes = new List<string>();

        foreach (var entry in Directory.EnumerateFileSystemEntries(fullDir))
        {
            var name = Path.GetFileName(entry);
            if (relDir == "_repolore" && (name == "sessions" || name == ".history"))
                continue;
            if (RepositoryPathResolver.IsReparsePoint(entry))
                continue;

            if (Directory.Exists(entry))
                dirs.Add(name);
            else if (name.EndsWith(".md", StringComparison.Ordinal))
                notes.Add(name);
        }

        dirs.Sort(StringComparer.Ordinal);
        notes.Sort(StringComparer.Ordinal);

        var entries = new List<TreeEntry>();
        foreach (var note in notes)
            entries.Add(new TreeEntry(relDir + "/" + note, false));
        foreach (var dir in dirs)
        {
            var below = Walk(Path.Combine(fullDir, dir), relDir + "/" + dir);
            if (below.Count == 0)
                continue;
            entries.Add(new TreeEntry(relDir + "/" + dir, true));
            entries.AddRange(below);
        }
        return entries;
    }

    // Session ids only, without opening any session.
    private static List<TreeEntry> ListSessions(string fullDir, string relDir)
    {
        var sessions = new List<string>();
        foreach (var entry in Directory.EnumerateDirectories(fullDir))
            if (!RepositoryPathResolver.IsReparsePoint(entry))
                sessions.Add(Path.GetFileName(entry));
        sessions.Sort(StringComparer.Ordinal);
        return sessions.Select(name => new TreeEntry(relDir + "/" + name, true)).ToList();
    }
}
