namespace RepoLore.Core.Context;

public enum CandidateKind { Method, Root, Ancestor, Target, SessionRoot, Node }

public sealed class ContextRequest
{
    public bool IncludeMethod { get; init; }
    public string? SourceTarget { get; init; }
    public string? SessionId { get; init; }
    public IReadOnlyList<string> Nodes { get; init; } = Array.Empty<string>();
}

public sealed class ContextCandidate
{
    public ContextCandidate(string canonicalPath, string? legacyPath, string scope, CandidateKind kind, bool required)
    {
        CanonicalPath = canonicalPath;
        LegacyPath = legacyPath;
        Scope = scope;
        Kind = kind;
        Required = required;
    }

    public string CanonicalPath { get; }
    public string? LegacyPath { get; }
    public string Scope { get; }
    public CandidateKind Kind { get; }
    public bool Required { get; }
}

public sealed class ContextSelection
{
    public List<ContextCandidate> Candidates { get; } = new();
}
