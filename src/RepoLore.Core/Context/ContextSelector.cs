using RepoLore.Core.Mapping;

namespace RepoLore.Core.Context;

public static class ContextSelector
{
    public const string DurableScope = "durable";
    public const string MethodPath = "_repolore/method.md";
    public const string RootPath = "_repolore/root.md";

    private const string SparseTreePrefix = "_repolore/sparse-tree/";
    private const string LegacyTreePrefix = "_repolore/tree/";
    private const string SessionsPrefix = "_repolore/sessions/";

    public static ContextSelection Select(ContextRequest request)
    {
        var result = new ContextSelection();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (request.IncludeMethod)
            Add(result, seen, new ContextCandidate(MethodPath, null, DurableScope, CandidateKind.Method, required: true));

        if (request.SourceTarget is not null)
            AddDurablePath(result, seen, request.SourceTarget);

        if (request.SessionId is not null)
        {
            var sessionRoot = SessionsPrefix + request.SessionId + "/root.md";
            Add(result, seen, new ContextCandidate(sessionRoot, null, SessionScope(request.SessionId), CandidateKind.SessionRoot, required: true));
        }

        foreach (var node in request.Nodes)
        {
            var info = KnowledgeNodePath.Classify(node);
            if (!info.IsValid)
                throw new ArgumentException(info.Finding ?? "invalid --node path");

            if (info.SessionId is not null)
            {
                if (request.SessionId is null)
                    throw new ArgumentException($"session note '{node}' requires --session {info.SessionId}");

                if (!string.Equals(info.SessionId, request.SessionId, StringComparison.Ordinal))
                    throw new ArgumentException($"session note '{node}' belongs to session '{info.SessionId}', not the selected '{request.SessionId}'");

                Add(result, seen, new ContextCandidate(node, null, SessionScope(info.SessionId), CandidateKind.Node, required: true));
            }
            else
            {
                Add(result, seen, new ContextCandidate(node, null, DurableScope, CandidateKind.Node, required: true));
            }
        }

        return result;
    }

    public static string SessionScope(string sessionId) => "session:" + sessionId;

    private static void AddDurablePath(ContextSelection result, HashSet<string> seen, string target)
    {
        Add(result, seen, new ContextCandidate(RootPath, null, DurableScope, CandidateKind.Root, required: true));

        var directories = SourceDirectories(target);
        for (var i = 0; i < directories.Count; i++)
        {
            var note = KnowledgePathMapper.MapDirectoryNote(directories[i]);
            var canonical = SparseTreePrefix + note;
            var legacy = LegacyTreePrefix + note;
            var kind = i == directories.Count - 1 ? CandidateKind.Target : CandidateKind.Ancestor;
            Add(result, seen, new ContextCandidate(canonical, legacy, DurableScope, kind, required: false));
        }
    }

    private static List<string> SourceDirectories(string target)
    {
        var result = new List<string>();
        var trimmed = target.TrimEnd('/');
        if (trimmed.Length == 0 || trimmed == ".")
            return result;

        var prefix = "";
        foreach (var segment in trimmed.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or "..")
                throw new ArgumentException($"source target contains an unsupported segment: '{segment}'");
            if (segment.Contains('\\') || segment.Contains(':'))
                throw new ArgumentException($"source target contains an unsupported segment: '{segment}'");

            prefix = prefix.Length == 0 ? segment : prefix + "/" + segment;
            result.Add(prefix + "/");
        }

        return result;
    }

    private static void Add(ContextSelection result, HashSet<string> seen, ContextCandidate candidate)
    {
        if (!seen.Add(candidate.CanonicalPath))
            return;
        result.Candidates.Add(candidate);
    }
}
