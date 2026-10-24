using RepoLore.Core.Context;

namespace RepoLore.Cli.Reading;

public sealed class ResolvedNote
{
    public string Path { get; init; } = "";
    public string Scope { get; init; } = "";
    public NoteStatus Status { get; init; }
    public string? Text { get; init; }
    public bool IsLegacy { get; init; }
    public string? Finding { get; init; }
}
