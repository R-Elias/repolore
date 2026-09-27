using RepoLore.Core.Context;
using RepoLore.Cli.Arguments;
using RepoLore.Cli.Reading;
using RepoLore.Cli.Rendering;
using RepoLore.Cli.Repository;

namespace RepoLore.Cli.Commands;

public static class ContextCommand
{
    public static int Run(CommandLine cl, string cwd)
    {
        var root = RepositoryRoot.Discover(cl.RepoRoot, cwd);

        if (cl.Session is not null && !SessionId.IsValid(cl.Session, out var sessionFinding))
            throw new UsageException("invalid --session: " + sessionFinding);

        if (cl.Budget <= 0)
            throw new UsageException("--budget-tokens must be a positive integer");

        var hasSelector = cl.Target is not null || cl.Session is not null || cl.Nodes.Count > 0;
        if (!hasSelector)
            throw new UsageException("context requires a target, --session, or --node");

        var request = new ContextRequest
        {
            IncludeMethod = cl.IncludeMethod,
            SourceTarget = cl.Target,
            SessionId = cl.Session,
            Nodes = cl.Nodes
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
        var notes = new List<ContextNote>();
        var findings = new List<Finding>();
        var hasConflict = false;

        foreach (var candidate in selection.Candidates)
        {
            var resolved = reader.Read(candidate);
            switch (resolved.Status)
            {
                case NoteStatus.Found:
                    notes.Add(new ContextNote(resolved.Path, resolved.Scope, resolved.Text!));
                    break;
                case NoteStatus.Empty:
                    break;
                case NoteStatus.Missing:
                    if (candidate.Required)
                    {
                        var isSessionRoot = candidate.Kind == CandidateKind.SessionRoot;
                        findings.Add(new Finding(
                            isSessionRoot ? "missing-session" : "missing-note",
                            candidate.CanonicalPath,
                            isSessionRoot ? $"session '{cl.Session}' does not exist" : "requested note does not exist"));
                    }
                    break;
                case NoteStatus.Conflict:
                    findings.Add(new Finding("conflict", candidate.CanonicalPath, resolved.Finding ?? "conflicting copies"));
                    hasConflict = true;
                    break;
                case NoteStatus.InvalidUtf8:
                    findings.Add(new Finding("invalid-utf8", resolved.Path, resolved.Finding ?? "not valid UTF-8"));
                    break;
            }
        }

        var estimate = BlockEstimator.Estimate(notes, cl.Budget);
        Renderer.Context(estimate, findings, cl.Json, cl.Quiet);

        if (hasConflict)
            return ExitCodes.Failure;
        if (findings.Count > 0)
            return ExitCodes.Findings;
        if (cl.Strict && estimate.Omissions.Count > 0)
            return ExitCodes.Findings;
        return ExitCodes.Success;
    }
}
