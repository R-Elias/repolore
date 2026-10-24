using FluentAssertions;
using RepoLore.Core.Json;
using RepoLore.Core.Snapshot;
using Xunit;

namespace RepoLore.Core.Tests.Snapshot;

public class ManifestIdTests
{
    [Fact]
    public void Format_and_parse_round_trip()
    {
        ManifestId.Format(1).Should().Be("0000000000000001");
        ManifestId.Format(42).Should().Be("0000000000000042");

        ManifestId.TryParse("0000000000000001.json", out var one).Should().BeTrue();
        one.Should().Be(1L);
        ManifestId.TryParse("0000000000000042.json", out var fortyTwo).Should().BeTrue();
        fortyTwo.Should().Be(42L);
    }

    [Fact]
    public void Non_manifest_names_are_rejected()
    {
        ManifestId.TryParse("0000000000000001", out _).Should().BeFalse();
        ManifestId.TryParse("1.json", out _).Should().BeFalse();
        ManifestId.TryParse("000000000000000x.json", out _).Should().BeFalse();
        ManifestId.TryParse("0000000000000001.tmp-abc", out _).Should().BeFalse();
        ManifestId.TryParse("00000000000000001.json", out _).Should().BeFalse();
        ManifestId.TryParse("pending.json", out _).Should().BeFalse();
    }
}

public class ManifestCodecTests
{
    [Fact]
    public void Encode_decode_round_trip_preserves_every_field()
    {
        var scope = new CaptureScope(true, 209715200, new[] { "_repolore/sessions/a/" }, CaptureScope.RepoLoreJsonPresent);
        var manifest = new CheckpointManifest(7, "2026-09-27T11:00:00.0000000+00:00", HistoryVersion.Current, 1, scope, new[]
        {
            new FileEntry("_repolore/root.md", "abc123", 12),
            new FileEntry("_repolore/sparse-tree/src/src.md", "def456", 34)
        });

        var decoded = ManifestCodec.Decode(ManifestCodec.Encode(manifest));

        decoded.Id.Should().Be(7L);
        decoded.Timestamp.Should().Be("2026-09-27T11:00:00.0000000+00:00");
        decoded.HistoryVersion.Should().Be(HistoryVersion.Current);
        decoded.KnowledgeFormat.Should().Be(1);
        decoded.Scope.HistoryEnabled.Should().BeTrue();
        decoded.Scope.MaxBytes.Should().Be(209715200L);
        decoded.Scope.RepoLoreJsonStatus.Should().Be(CaptureScope.RepoLoreJsonPresent);
        decoded.Scope.ExcludeRules.Should().HaveCount(1);
        decoded.Scope.ExcludeRules[0].Should().Be("_repolore/sessions/a/");
        decoded.Files.Should().HaveCount(2);
        decoded.Files[0].Path.Should().Be("_repolore/root.md");
        decoded.Files[0].Hash.Should().Be("abc123");
        decoded.Files[0].Size.Should().Be(12L);
    }

    [Fact]
    public void Non_object_and_missing_field_manifests_are_rejected()
    {
        new Action(() => ManifestCodec.Decode(JsonParser.Parse("[1,2]"))).Should().Throw<ManifestFormatException>();
        new Action(() => ManifestCodec.Decode(JsonParser.Parse("{}"))).Should().Throw<ManifestFormatException>();
        new Action(() => ManifestCodec.Decode(JsonParser.Parse("{\"historyVersion\":1}"))).Should().Throw<ManifestFormatException>();
    }

    [Fact]
    public void Wrong_types_and_unknown_versions_are_rejected()
    {
        new Action(() => ManifestCodec.Decode(With(Valid(), "historyVersion", new JsonStringValue("1")))).Should().Throw<ManifestFormatException>();
        new Action(() => ManifestCodec.Decode(With(Valid(), "historyVersion", new JsonNumberValue("2")))).Should().Throw<ManifestFormatException>();
        new Action(() => ManifestCodec.Decode(With(Valid(), "id", new JsonStringValue("3")))).Should().Throw<ManifestFormatException>();
        new Action(() => ManifestCodec.Decode(With(Valid(), "id", new JsonNumberValue("-1")))).Should().Throw<ManifestFormatException>();
        new Action(() => ManifestCodec.Decode(With(Valid(), "timestamp", new JsonNumberValue("0")))).Should().Throw<ManifestFormatException>();
        new Action(() => ManifestCodec.Decode(With(Valid(), "files", new JsonStringValue("x")))).Should().Throw<ManifestFormatException>();
    }

    [Fact]
    public void Malformed_scope_is_rejected()
    {
        new Action(() => ManifestCodec.Decode(With(Valid(), "scope", new JsonNumberValue("0")))).Should().Throw<ManifestFormatException>();
        new Action(() => ManifestCodec.Decode(WithScopeField("maxBytes", new JsonNumberValue("-1")))).Should().Throw<ManifestFormatException>();
        new Action(() => ManifestCodec.Decode(WithScopeField("repoloreJson", new JsonStringValue("weird")))).Should().Throw<ManifestFormatException>();
        new Action(() => ManifestCodec.Decode(WithScopeField("historyEnabled", new JsonStringValue("yes")))).Should().Throw<ManifestFormatException>();
        new Action(() => ManifestCodec.Decode(WithScopeField("exclude", new JsonStringValue("a")))).Should().Throw<ManifestFormatException>();
    }

    [Fact]
    public void Malformed_file_entries_are_rejected()
    {
        new Action(() => ManifestCodec.Decode(With(Valid(), "files", JsonParser.Parse("[{\"path\":\"a\",\"hash\":\"h\",\"size\":-1}]")))).Should().Throw<ManifestFormatException>();
        new Action(() => ManifestCodec.Decode(With(Valid(), "files", JsonParser.Parse("[1]")))).Should().Throw<ManifestFormatException>();
        new Action(() => ManifestCodec.Decode(With(Valid(), "files", JsonParser.Parse("[{\"path\":\"a\"}]")))).Should().Throw<ManifestFormatException>();
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

public class SnapshotDifferTests
{
    private static readonly CaptureScope Scope = new(true, 100, new List<string>(), CaptureScope.RepoLoreJsonPresent);

    [Fact]
    public void First_checkpoint_is_never_a_no_op_and_reports_all_added()
    {
        var diff = SnapshotDiffer.Compare(null, Scope, Files(("_repolore/root.md", "h1"), ("_repolore/a.md", "h2")));
        diff.IsNoOp.Should().BeFalse();
        string.Join(',', diff.Added).Should().Be("_repolore/a.md,_repolore/root.md");
        diff.Changed.Should().HaveCount(0);
        diff.Removed.Should().HaveCount(0);
    }

    [Fact]
    public void Unchanged_files_and_scope_are_a_no_op()
    {
        var previous = Manifest(Files(("_repolore/root.md", "h1")));
        var diff = SnapshotDiffer.Compare(previous, Scope, Files(("_repolore/root.md", "h1")));
        diff.IsNoOp.Should().BeTrue();
    }

    [Fact]
    public void Additions_changes_and_removals_are_classified()
    {
        var previous = Manifest(Files(("a.md", "hA"), ("b.md", "hB"), ("c.md", "hC")));
        var diff = SnapshotDiffer.Compare(previous, Scope, Files(("a.md", "hA2"), ("b.md", "hB"), ("d.md", "hD")));
        diff.IsNoOp.Should().BeFalse();
        string.Join(',', diff.Added).Should().Be("d.md");
        string.Join(',', diff.Changed).Should().Be("a.md");
        string.Join(',', diff.Removed).Should().Be("c.md");
    }

    [Fact]
    public void A_scope_only_change_is_recorded_even_when_hashes_match()
    {
        var previous = Manifest(Files(("a.md", "hA")));
        var wider = new CaptureScope(true, 200, new List<string>(), CaptureScope.RepoLoreJsonPresent);
        var diff = SnapshotDiffer.Compare(previous, wider, Files(("a.md", "hA")));
        diff.IsNoOp.Should().BeFalse();
        diff.Added.Should().HaveCount(0);
        diff.Changed.Should().HaveCount(0);
        diff.Removed.Should().HaveCount(0);
    }

    [Fact]
    public void Exclude_rule_and_repoloreJson_status_changes_are_scope_changes()
    {
        var previous = Manifest(Files(("a.md", "hA")));

        var excluded = new CaptureScope(true, 100, new[] { "_repolore/sessions/x/" }, CaptureScope.RepoLoreJsonPresent);
        SnapshotDiffer.Compare(previous, excluded, Files(("a.md", "hA"))).IsNoOp.Should().BeFalse();

        var absent = new CaptureScope(true, 100, new List<string>(), CaptureScope.RepoLoreJsonAbsent);
        SnapshotDiffer.Compare(previous, absent, Files(("a.md", "hA"))).IsNoOp.Should().BeFalse();
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
