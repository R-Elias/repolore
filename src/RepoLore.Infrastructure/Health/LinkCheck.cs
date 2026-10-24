using RepoLore.Core.Context;
using RepoLore.Core.Health;

namespace RepoLore.Infrastructure.Health;

public static class LinkCheck
{
    private const string SessionsPrefix = "_repolore/sessions";

    public static void CheckDurable(string repositoryRoot, List<Finding> findings)
    {
        var resolver = new RepositoryPathResolver(repositoryRoot);
        var repoLore = Path.Combine(repositoryRoot, "_repolore");
        foreach (var note in EnumerateNotes(repoLore, "_repolore", skipReservedAreas: true))
            CheckNote(repositoryRoot, resolver, note, null, findings);
    }

    public static void CheckSession(string repositoryRoot, string sessionId, string sessionDirectory, List<Finding> findings)
    {
        var resolver = new RepositoryPathResolver(repositoryRoot);
        foreach (var note in EnumerateNotes(sessionDirectory, SessionsPrefix + "/" + sessionId, skipReservedAreas: false))
            CheckNote(repositoryRoot, resolver, note, sessionId, findings);
    }

    private static List<string> EnumerateNotes(string directory, string relativeDirectory, bool skipReservedAreas)
    {
        var notes = new List<string>();
        Walk(directory, relativeDirectory, skipReservedAreas, notes);
        notes.Sort(StringComparer.Ordinal);
        return notes;
    }

    private static void Walk(string directory, string relativeDirectory, bool skipReservedAreas, List<string> notes)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            var name = Path.GetFileName(entry);
            var relative = relativeDirectory + "/" + name;
            if (RepositoryPathResolver.IsReparsePoint(entry))
                continue;
            if (skipReservedAreas && relativeDirectory == "_repolore" && name is "sessions" or ".history")
                continue;

            if (Directory.Exists(entry))
                Walk(entry, relative, skipReservedAreas, notes);
            else if (name.EndsWith(".md", StringComparison.Ordinal))
                notes.Add(relative);
        }
    }

    private static void CheckNote(string repositoryRoot, RepositoryPathResolver resolver, string note, string? ownSessionId, List<Finding> findings)
    {
        var bytes = File.ReadAllBytes(Path.Combine(repositoryRoot, note));
        if (!NoteContent.TryNormalize(bytes, out var text, out _))
        {
            findings.Add(new Finding(FindingIds.InvalidUtf8, note, "note is not valid UTF-8"));
            return;
        }

        foreach (var target in MarkdownLinks.ExtractLocalTargets(text!))
            CheckTarget(resolver, note, target, ownSessionId, findings);
    }

    private static void CheckTarget(RepositoryPathResolver resolver, string note, string target, string? ownSessionId, List<Finding> findings)
    {
        var resolved = Combine(note, target);
        if (resolved is null)
        {
            findings.Add(new Finding(FindingIds.BrokenLink, note, $"link '{target}' escapes the repository"));
            return;
        }

        var inSessions = resolved == SessionsPrefix || resolved.StartsWith(SessionsPrefix + "/", StringComparison.Ordinal);
        if (inSessions && ownSessionId is null)
        {
            findings.Add(new Finding(FindingIds.SessionLinkInDurableNote, note, $"durable note links to local-only session content '{resolved}'"));
            return;
        }
        var ownPrefix = SessionsPrefix + "/" + ownSessionId;
        if (inSessions && resolved != ownPrefix && !resolved.StartsWith(ownPrefix + "/", StringComparison.Ordinal))
            return;

        try
        {
            var full = resolver.Resolve(resolved);
            if (!File.Exists(full) && !Directory.Exists(full))
                findings.Add(new Finding(FindingIds.BrokenLink, note, $"link '{target}' does not exist: {resolved}"));
        }
        catch (PathResolutionException ex)
        {
            findings.Add(new Finding(FindingIds.BrokenLink, note, $"link '{target}' is not a valid repository path: {ex.Message}"));
        }
    }

    private static string? Combine(string note, string target)
    {
        var segments = new List<string>(note.Split('/'));
        segments.RemoveAt(segments.Count - 1);

        foreach (var part in target.Split('/'))
        {
            if (part.Length == 0 || part == ".")
                continue;
            if (part != "..")
            {
                segments.Add(part);
                continue;
            }
            if (segments.Count == 0)
                return null;
            segments.RemoveAt(segments.Count - 1);
        }

        return segments.Count == 0 ? null : string.Join('/', segments);
    }
}
