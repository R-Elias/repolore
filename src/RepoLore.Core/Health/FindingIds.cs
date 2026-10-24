namespace RepoLore.Core.Health;

public static class FindingIds
{
    public const string MissingNote = "missing-note";
    public const string InvalidUtf8 = "invalid-utf8";
    public const string InvalidNoteName = "invalid-note-name";
    public const string BrokenLink = "broken-link";
    public const string OrphanPathNote = "orphan-path-note";
    public const string InvalidSession = "invalid-session";
    public const string SessionLinkInDurableNote = "session-link-in-durable-note";
    public const string MigrationNeeded = "migration-needed";
    public const string MalformedFormat = "malformed-format";
    public const string UnsupportedFormat = "unsupported-format";
    public const string PolicyError = "policy-error";
    public const string ConflictingAlphaNotes = "conflicting-alpha-notes";
    public const string HistoryUnavailable = "history-unavailable";
    public const string HistoryCorrupt = "history-corrupt";
    public const string HistoryPending = "history-pending";

    public static readonly IReadOnlyList<string> All = new[]
    {
        MissingNote, InvalidUtf8, InvalidNoteName, BrokenLink, OrphanPathNote, InvalidSession,
        SessionLinkInDurableNote, MigrationNeeded, MalformedFormat, UnsupportedFormat, PolicyError,
        ConflictingAlphaNotes, HistoryUnavailable, HistoryCorrupt, HistoryPending
    };

    public static bool IsBlocking(string id) =>
        id is MalformedFormat or UnsupportedFormat or PolicyError or ConflictingAlphaNotes
            or HistoryUnavailable or HistoryCorrupt or HistoryPending;
}
