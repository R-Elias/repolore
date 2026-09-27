using RepoLore.Core.Context;
using RepoLore.Infrastructure;

namespace RepoLore.Cli.Reading;

public sealed class KnowledgeReader
{
    private readonly RepositoryPathResolver _resolver;

    public KnowledgeReader(string repositoryRoot)
    {
        _resolver = new RepositoryPathResolver(repositoryRoot);
    }

    public ResolvedNote Read(ContextCandidate candidate)
    {
        var primary = ReadFile(candidate.CanonicalPath);
        var legacy = candidate.LegacyPath is null ? NoteFileState.Absent : ReadFile(candidate.LegacyPath);
        var decision = NoteReadResolver.Resolve(primary, legacy);

        var path = decision.IsLegacy && candidate.LegacyPath is not null
            ? candidate.LegacyPath
            : candidate.CanonicalPath;

        return new ResolvedNote
        {
            Path = path,
            Scope = candidate.Scope,
            Status = decision.Status,
            Text = decision.Text,
            IsLegacy = decision.IsLegacy,
            Finding = decision.Finding
        };
    }

    private NoteFileState ReadFile(string relativePath)
    {
        string full;
        try
        {
            full = _resolver.Resolve(relativePath);
        }
        catch (PathResolutionException)
        {
            return NoteFileState.Absent;
        }

        if (!File.Exists(full))
            return NoteFileState.Absent;

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(full);
        }
        catch (IOException)
        {
            return NoteFileState.Absent;
        }
        catch (UnauthorizedAccessException)
        {
            return NoteFileState.Absent;
        }

        if (!NoteContent.TryNormalize(bytes, out var text, out _))
            return NoteFileState.InvalidUtf8();

        return NoteContent.IsEmpty(text!) ? NoteFileState.Empty() : NoteFileState.Content(text!);
    }
}
