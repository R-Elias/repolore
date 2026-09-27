namespace RepoLore.Core.Context;

public enum NoteFileKind { Absent, Empty, Content, InvalidUtf8 }

public sealed class NoteFileState
{
    public NoteFileState(NoteFileKind kind, string? text)
    {
        Kind = kind;
        Text = text;
    }

    public NoteFileKind Kind { get; }
    public string? Text { get; }

    public static readonly NoteFileState Absent = new(NoteFileKind.Absent, null);

    public static NoteFileState Empty() => new(NoteFileKind.Empty, null);

    public static NoteFileState Content(string text) => new(NoteFileKind.Content, text);

    public static NoteFileState InvalidUtf8() => new(NoteFileKind.InvalidUtf8, null);
}

public enum NoteStatus { Found, Empty, Missing, Conflict, InvalidUtf8 }

public sealed class NoteReadResult
{
    public NoteStatus Status { get; init; }
    public string? Text { get; init; }
    public bool IsLegacy { get; init; }
    public string? Finding { get; init; }
}

public static class NoteReadResolver
{
    public static NoteReadResult Resolve(NoteFileState primary, NoteFileState legacy)
    {
        if (primary.Kind == NoteFileKind.InvalidUtf8)
            return Make(NoteStatus.InvalidUtf8, null, false, "note is not valid UTF-8");

        if (legacy.Kind == NoteFileKind.InvalidUtf8
            && primary.Kind is NoteFileKind.Absent or NoteFileKind.Empty)
            return Make(NoteStatus.InvalidUtf8, null, true, "note is not valid UTF-8");

        var primaryContent = primary.Kind == NoteFileKind.Content;
        var legacyContent = legacy.Kind == NoteFileKind.Content;

        if (primaryContent && legacyContent
            && !string.Equals(primary.Text, legacy.Text, StringComparison.Ordinal))
            return Make(NoteStatus.Conflict, null, false, "alpha tree/ and sparse-tree/ copies differ");

        if (primaryContent)
            return Make(NoteStatus.Found, primary.Text, false, null);

        if (legacyContent)
            return Make(NoteStatus.Found, legacy.Text, true, null);

        if (primary.Kind == NoteFileKind.Empty || legacy.Kind == NoteFileKind.Empty)
            return Make(NoteStatus.Empty, null, false, null);

        return Make(NoteStatus.Missing, null, false, null);
    }

    private static NoteReadResult Make(NoteStatus status, string? text, bool isLegacy, string? finding) =>
        new() { Status = status, Text = text, IsLegacy = isLegacy, Finding = finding };
}
