using RepoLore.Core.Json;
using RepoLore.Core.Snapshot;
using RepoLore.Core.Tests;

namespace RepoLore.Core.Tests.Snapshot;

public static class ManifestIdTests
{
    public static void Run()
    {
        TestRunner.Check("format and parse round-trip", () =>
        {
            TestRunner.Equal("0000000000000001", ManifestId.Format(1));
            TestRunner.Equal("0000000000000042", ManifestId.Format(42));

            TestRunner.True(ManifestId.TryParse("0000000000000001.json", out var one));
            TestRunner.Equal(1L, one);
            TestRunner.True(ManifestId.TryParse("0000000000000042.json", out var fortyTwo));
            TestRunner.Equal(42L, fortyTwo);
        });

        TestRunner.Check("non-manifest names are rejected", () =>
        {
            TestRunner.True(!ManifestId.TryParse("0000000000000001", out _));
            TestRunner.True(!ManifestId.TryParse("1.json", out _));
            TestRunner.True(!ManifestId.TryParse("000000000000000x.json", out _));
            TestRunner.True(!ManifestId.TryParse("0000000000000001.tmp-abc", out _));
            TestRunner.True(!ManifestId.TryParse("00000000000000001.json", out _));
            TestRunner.True(!ManifestId.TryParse("pending.json", out _));
        });
    }
}

public static class ManifestCodecTests
{
    public static void Run()
    {
        TestRunner.Check("encode/decode round-trip preserves every field", () =>
        {
            var scope = new CaptureScope(true, 209715200, new[] { "_repolore/sessions/a/" }, CaptureScope.RepoLoreJsonPresent);
            var manifest = new CheckpointManifest(7, "2026-09-27T11:00:00.0000000+00:00", HistoryVersion.Current, 1, scope, new[]
            {
                new FileEntry("_repolore/root.md", "abc123", 12),
                new FileEntry("_repolore/sparse-tree/src/src.md", "def456", 34)
            });

            var decoded = ManifestCodec.Decode(ManifestCodec.Encode(manifest));

            TestRunner.Equal(7L, decoded.Id);
            TestRunner.Equal("2026-09-27T11:00:00.0000000+00:00", decoded.Timestamp);
            TestRunner.Equal(HistoryVersion.Current, decoded.HistoryVersion);
            TestRunner.Equal(1, decoded.KnowledgeFormat);
            TestRunner.Equal(true, decoded.Scope.HistoryEnabled);
            TestRunner.Equal(209715200L, decoded.Scope.MaxBytes);
            TestRunner.Equal(CaptureScope.RepoLoreJsonPresent, decoded.Scope.RepoLoreJsonStatus);
            TestRunner.Equal(1, decoded.Scope.ExcludeRules.Count);
            TestRunner.Equal("_repolore/sessions/a/", decoded.Scope.ExcludeRules[0]);
            TestRunner.Equal(2, decoded.Files.Count);
            TestRunner.Equal("_repolore/root.md", decoded.Files[0].Path);
            TestRunner.Equal("abc123", decoded.Files[0].Hash);
            TestRunner.Equal(12L, decoded.Files[0].Size);
        });

        TestRunner.Check("non-object and missing-field manifests are rejected", () =>
        {
            TestRunner.Throws<ManifestFormatException>(() => ManifestCodec.Decode(JsonParser.Parse("[1,2]")));
            TestRunner.Throws<ManifestFormatException>(() => ManifestCodec.Decode(JsonParser.Parse("{}")));
            TestRunner.Throws<ManifestFormatException>(() => ManifestCodec.Decode(JsonParser.Parse("{\"historyVersion\":1}")));
        });

        TestRunner.Check("wrong types and unknown versions are rejected", () =>
        {
            TestRunner.Throws<ManifestFormatException>(() => ManifestCodec.Decode(With(Valid(), "historyVersion", new JsonStringValue("1"))));
            TestRunner.Throws<ManifestFormatException>(() => ManifestCodec.Decode(With(Valid(), "historyVersion", new JsonNumberValue("2"))));
            TestRunner.Throws<ManifestFormatException>(() => ManifestCodec.Decode(With(Valid(), "id", new JsonStringValue("3"))));
            TestRunner.Throws<ManifestFormatException>(() => ManifestCodec.Decode(With(Valid(), "id", new JsonNumberValue("-1"))));
            TestRunner.Throws<ManifestFormatException>(() => ManifestCodec.Decode(With(Valid(), "timestamp", new JsonNumberValue("0"))));
            TestRunner.Throws<ManifestFormatException>(() => ManifestCodec.Decode(With(Valid(), "files", new JsonStringValue("x"))));
        });

        TestRunner.Check("malformed scope is rejected", () =>
        {
            TestRunner.Throws<ManifestFormatException>(() => ManifestCodec.Decode(With(Valid(), "scope", new JsonNumberValue("0"))));
            TestRunner.Throws<ManifestFormatException>(() => ManifestCodec.Decode(WithScopeField("maxBytes", new JsonNumberValue("-1"))));
            TestRunner.Throws<ManifestFormatException>(() => ManifestCodec.Decode(WithScopeField("repoloreJson", new JsonStringValue("weird"))));
            TestRunner.Throws<ManifestFormatException>(() => ManifestCodec.Decode(WithScopeField("historyEnabled", new JsonStringValue("yes"))));
            TestRunner.Throws<ManifestFormatException>(() => ManifestCodec.Decode(WithScopeField("exclude", new JsonStringValue("a"))));
        });

        TestRunner.Check("malformed file entries are rejected", () =>
        {
            TestRunner.Throws<ManifestFormatException>(() => ManifestCodec.Decode(With(Valid(), "files", JsonParser.Parse("[{\"path\":\"a\",\"hash\":\"h\",\"size\":-1}]"))));
            TestRunner.Throws<ManifestFormatException>(() => ManifestCodec.Decode(With(Valid(), "files", JsonParser.Parse("[1]"))));
            TestRunner.Throws<ManifestFormatException>(() => ManifestCodec.Decode(With(Valid(), "files", JsonParser.Parse("[{\"path\":\"a\"}]"))));
        });
    }

    private static JsonObject Valid()
    {
        var scope = new CaptureScope(true, 100, new List<string>(), CaptureScope.RepoLoreJsonPresent);
        var manifest = new CheckpointManifest(3, "t", HistoryVersion.Current, 1, scope, new List<FileEntry>());
        return (JsonObject)ManifestCodec.Encode(manifest);
    }

    private static JsonValue With(JsonObject source, string name, JsonValue value)
    {
        var copy = new JsonObject();
        foreach (var member in source.Members)
            copy.Members.Add(new JsonMember(member.Name, string.Equals(member.Name, name, StringComparison.Ordinal) ? value : member.Value));
        return copy;
    }

    private static JsonValue WithScopeField(string name, JsonValue value)
    {
        var valid = Valid();
        var scope = Required(valid, "scope");
        var newScope = new JsonObject();
        foreach (var member in ((JsonObject)scope).Members)
            newScope.Members.Add(new JsonMember(member.Name, string.Equals(member.Name, name, StringComparison.Ordinal) ? value : member.Value));
        return With(valid, "scope", newScope);
    }

    private static JsonValue Required(JsonObject obj, string name)
    {
        foreach (var member in obj.Members)
            if (string.Equals(member.Name, name, StringComparison.Ordinal))
                return member.Value;
        throw new InvalidOperationException("missing field " + name);
    }
}

public static class SnapshotDifferTests
{
    private static readonly CaptureScope Scope = new(true, 100, new List<string>(), CaptureScope.RepoLoreJsonPresent);

    public static void Run()
    {
        TestRunner.Check("first checkpoint is never a no-op and reports all added", () =>
        {
            var diff = SnapshotDiffer.Compare(null, Scope, Files(("_repolore/root.md", "h1"), ("_repolore/a.md", "h2")));
            TestRunner.True(!diff.IsNoOp);
            TestRunner.Equal("_repolore/a.md,_repolore/root.md", string.Join(',', diff.Added));
            TestRunner.Equal(0, diff.Changed.Count);
            TestRunner.Equal(0, diff.Removed.Count);
        });

        TestRunner.Check("unchanged files and scope are a no-op", () =>
        {
            var previous = Manifest(Files(("_repolore/root.md", "h1")));
            var diff = SnapshotDiffer.Compare(previous, Scope, Files(("_repolore/root.md", "h1")));
            TestRunner.True(diff.IsNoOp);
        });

        TestRunner.Check("additions, changes, and removals are classified", () =>
        {
            var previous = Manifest(Files(("a.md", "hA"), ("b.md", "hB"), ("c.md", "hC")));
            var diff = SnapshotDiffer.Compare(previous, Scope, Files(("a.md", "hA2"), ("b.md", "hB"), ("d.md", "hD")));
            TestRunner.True(!diff.IsNoOp);
            TestRunner.Equal("d.md", string.Join(',', diff.Added));
            TestRunner.Equal("a.md", string.Join(',', diff.Changed));
            TestRunner.Equal("c.md", string.Join(',', diff.Removed));
        });

        TestRunner.Check("a scope-only change is recorded even when hashes match", () =>
        {
            var previous = Manifest(Files(("a.md", "hA")));
            var wider = new CaptureScope(true, 200, new List<string>(), CaptureScope.RepoLoreJsonPresent);
            var diff = SnapshotDiffer.Compare(previous, wider, Files(("a.md", "hA")));
            TestRunner.True(!diff.IsNoOp);
            TestRunner.Equal(0, diff.Added.Count);
            TestRunner.Equal(0, diff.Changed.Count);
            TestRunner.Equal(0, diff.Removed.Count);
        });

        TestRunner.Check("exclude-rule and repoloreJson status changes are scope changes", () =>
        {
            var previous = Manifest(Files(("a.md", "hA")));

            var excluded = new CaptureScope(true, 100, new[] { "_repolore/sessions/x/" }, CaptureScope.RepoLoreJsonPresent);
            TestRunner.True(!SnapshotDiffer.Compare(previous, excluded, Files(("a.md", "hA"))).IsNoOp);

            var absent = new CaptureScope(true, 100, new List<string>(), CaptureScope.RepoLoreJsonAbsent);
            TestRunner.True(!SnapshotDiffer.Compare(previous, absent, Files(("a.md", "hA"))).IsNoOp);
        });
    }

    private static IReadOnlyList<FileEntry> Files(params (string Path, string Hash)[] entries)
    {
        var list = new List<FileEntry>(entries.Length);
        foreach (var (path, hash) in entries)
            list.Add(new FileEntry(path, hash, 1));
        list.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return list;
    }

    private static CheckpointManifest Manifest(IReadOnlyList<FileEntry> files) =>
        new(1, "t", HistoryVersion.Current, 1, Scope, files);
}
