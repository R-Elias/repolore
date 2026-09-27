namespace RepoLore.Core.Context;

public sealed class NodePathInfo
{
    public bool IsValid { get; init; }
    public string? SessionId { get; init; }
    public string? Finding { get; init; }
}

public static class KnowledgeNodePath
{
    private const string RepoLorePrefix = "_repolore/";
    private const string SessionsPrefix = "_repolore/sessions/";
    private const string HistoryDirectory = "_repolore/.history";

    public static NodePathInfo Classify(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\\'))
            return Invalid("node path must be a repository-relative '/'-separated path under _repolore/");

        if (!path.StartsWith(RepoLorePrefix, StringComparison.Ordinal))
            return Invalid("node path must be under _repolore/");

        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0)
                return Invalid($"node path contains an empty segment: {path}");
            if (segment is "." or "..")
                return Invalid($"node path contains an unsupported segment: {segment}");
        }

        if (!path.EndsWith(".md", StringComparison.Ordinal))
            return Invalid("node path must name a Markdown note (.md)");

        if (path == HistoryDirectory || path.StartsWith(HistoryDirectory + "/", StringComparison.Ordinal))
            return Invalid("history internals cannot be read through context");

        if (path.StartsWith(SessionsPrefix, StringComparison.Ordinal))
        {
            var rest = path[SessionsPrefix.Length..];
            var slash = rest.IndexOf('/');
            if (slash < 0)
                return Invalid("session note path must name a note inside the session, not the session folder");

            var id = rest[..slash];
            var note = rest[(slash + 1)..];
            if (note.Length == 0)
                return Invalid("session note path must name a note inside the session");

            if (!SessionId.IsValid(id, out var finding))
                return Invalid($"invalid session id in node path: {finding}");

            return new NodePathInfo { IsValid = true, SessionId = id };
        }

        return new NodePathInfo { IsValid = true };
    }

    private static NodePathInfo Invalid(string finding) => new() { IsValid = false, Finding = finding };
}
