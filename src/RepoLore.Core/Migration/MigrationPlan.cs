using RepoLore.Core.Context;
using RepoLore.Core.Mapping;

namespace RepoLore.Core.Migration;

public enum NoteSource
{
    Tree,
    Sparse,
    TopRoot
}

public sealed class NoteVariant
{
    public NoteVariant(NoteSource source, string physicalPath, string alphaRelative, string normalizedText)
    {
        Source = source;
        PhysicalPath = physicalPath;
        AlphaRelative = alphaRelative;
        NormalizedText = normalizedText;
    }

    public NoteSource Source { get; }
    public string PhysicalPath { get; }
    public string AlphaRelative { get; }
    public string NormalizedText { get; }

    public bool IsUseful => !NoteContent.IsEmpty(NormalizedText);
}

public enum MappingStatus
{
    Candidate,
    Conflict,
    Ambiguous
}

public sealed class MigrationMapping
{
    public MigrationMapping(string key, string destination, MappingStatus status, string? sourcePath, string? normalizedText, string? finding)
    {
        Key = key;
        Destination = destination;
        Status = status;
        SourcePath = sourcePath;
        NormalizedText = normalizedText;
        Finding = finding;
    }

    public string Key { get; }
    public string Destination { get; }
    public MappingStatus Status { get; }
    public string? SourcePath { get; }
    public string? NormalizedText { get; }
    public string? Finding { get; }

    public bool NeedsCopy => Status == MappingStatus.Candidate && SourcePath is not null;
}

public static class MigrationPlanner
{
    private const string RootKey = "root";
    private const string SparseTreePrefix = "_repolore/sparse-tree/";
    private const string RootDestination = "_repolore/root.md";

    public static IReadOnlyList<MigrationMapping> Classify(IReadOnlyList<NoteVariant> notes)
    {
        var byKey = new Dictionary<string, List<NoteVariant>>(StringComparer.Ordinal);
        var ambiguous = new List<MigrationMapping>();

        foreach (var note in notes)
        {
            if (!KnowledgePathMapper.TryDecode(note.AlphaRelative, out var sourcePath, out var finding))
            {
                ambiguous.Add(new MigrationMapping(note.PhysicalPath, note.PhysicalPath, MappingStatus.Ambiguous, null, null, finding));
                continue;
            }

            var key = sourcePath == "." ? RootKey : sourcePath;
            if (!byKey.TryGetValue(key, out var list))
            {
                list = new List<NoteVariant>();
                byKey[key] = list;
            }
            list.Add(note);
        }

        var mappings = new List<MigrationMapping>(byKey.Count + ambiguous.Count);
        foreach (var key in byKey.Keys.OrderBy(static k => k, StringComparer.Ordinal))
            mappings.Add(ClassifyKey(key, byKey[key]));
        mappings.AddRange(ambiguous);

        return mappings;
    }

    private static MigrationMapping ClassifyKey(string key, List<NoteVariant> variants)
    {
        var destination = key == RootKey ? RootDestination : SparseTreePrefix + KnowledgePathMapper.MapDirectoryNote(key);

        var useful = new List<NoteVariant>(variants.Count);
        var texts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variant in variants)
        {
            if (!variant.IsUseful)
                continue;
            useful.Add(variant);
            texts.Add(variant.NormalizedText);
        }

        if (texts.Count == 0)
            return new MigrationMapping(key, destination, MappingStatus.Candidate, null, null, null);

        if (texts.Count > 1)
            return new MigrationMapping(key, destination, MappingStatus.Conflict, null, null, "alpha tree/ and sparse-tree/ copies differ");

        var winner = PreferredWinner(key, useful);
        var atDestination = key == RootKey
            ? winner.Source == NoteSource.TopRoot
            : winner.Source == NoteSource.Sparse;

        return new MigrationMapping(
            key,
            destination,
            MappingStatus.Candidate,
            atDestination ? null : winner.PhysicalPath,
            winner.NormalizedText,
            null);
    }

    private static NoteVariant PreferredWinner(string key, List<NoteVariant> useful)
    {
        if (key == RootKey)
        {
            foreach (var variant in useful)
                if (variant.Source == NoteSource.TopRoot)
                    return variant;
            foreach (var variant in useful)
                if (variant.Source == NoteSource.Sparse)
                    return variant;
            return useful[0];
        }

        foreach (var variant in useful)
            if (variant.Source == NoteSource.Sparse)
                return variant;
        return useful[0];
    }
}
