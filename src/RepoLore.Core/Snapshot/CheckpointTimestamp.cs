using System.Globalization;

namespace RepoLore.Core.Snapshot;

// Checkpoint times are ISO 8601 with exactly six fractional digits: the round-trip format "o" writes seven, which
// common parsers reject (Python before 3.11, for one). Older manifests hold seven and are shown normalized.
public static class CheckpointTimestamp
{
    private const string Pattern = "yyyy-MM-dd'T'HH:mm:ss.ffffffzzz";

    public static string Format(DateTimeOffset value) => value.ToString(Pattern, CultureInfo.InvariantCulture);

    public static string Normalize(string stored) =>
        DateTimeOffset.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? Format(parsed)
            : stored;
}
