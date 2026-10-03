using FluentAssertions;
using RepoLore.Core.Json;
using RepoLore.Core.Restore;
using RepoLore.Core.Snapshot;
using Xunit;

namespace RepoLore.Core.Tests.Restore;

public class PendingCodecTests
{
    [Fact]
    public void Encode_decode_round_trip_preserves_every_field()
    {
        var transaction = new PendingTransaction(
            HistoryVersion.Current,
            targetId: 7,
            preOperationId: 3,
            formatVersion: 1,
            historyEnabled: true,
            maxBytes: 1234,
            excludeRules: new[] { "_repolore/sessions/a/" },
            plan: new[]
            {
                new PendingPlanEntry("_repolore/a.md", "oldHash", "newHash"),
                new PendingPlanEntry("_repolore/b.md", null, "newHash"),
                new PendingPlanEntry("_repolore/c.md", "oldHash", null)
            });

        var decoded = PendingCodec.Decode(PendingCodec.Encode(transaction));

        decoded.HistoryVersion.Should().Be(HistoryVersion.Current);
        decoded.TargetId.Should().Be(7L);
        decoded.PreOperationId.Should().Be(3L);
        decoded.FormatVersion.Should().Be(1);
        decoded.HistoryEnabled.Should().BeTrue();
        decoded.MaxBytes.Should().Be(1234L);
        decoded.ExcludeRules.Should().HaveCount(1);
        decoded.ExcludeRules[0].Should().Be("_repolore/sessions/a/");
        decoded.Plan.Should().HaveCount(3);
        decoded.Plan[0].Path.Should().Be("_repolore/a.md");
        decoded.Plan[0].Before.Should().Be("oldHash");
        decoded.Plan[0].After.Should().Be("newHash");
        decoded.Plan[1].Before.Should().BeNull();
        decoded.Plan[2].After.Should().BeNull();
    }

    [Fact]
    public void Non_object_and_missing_field_records_are_rejected()
    {
        new Action(() => PendingCodec.Decode(JsonParser.Parse("[1,2]"))).Should().Throw<PendingFormatException>();
        new Action(() => PendingCodec.Decode(JsonParser.Parse("{}"))).Should().Throw<PendingFormatException>();
        new Action(() => PendingCodec.Decode(JsonParser.Parse("{\"historyVersion\":1}"))).Should().Throw<PendingFormatException>();
    }

    [Fact]
    public void Wrong_types_and_unknown_versions_are_rejected()
    {
        new Action(() => PendingCodec.Decode(With(Valid(), "historyVersion", new JsonStringValue("1")))).Should().Throw<PendingFormatException>();
        new Action(() => PendingCodec.Decode(With(Valid(), "historyVersion", new JsonNumberValue("2")))).Should().Throw<PendingFormatException>();
        new Action(() => PendingCodec.Decode(With(Valid(), "targetId", new JsonStringValue("3")))).Should().Throw<PendingFormatException>();
        new Action(() => PendingCodec.Decode(With(Valid(), "preOperationId", new JsonNumberValue("-1")))).Should().Throw<PendingFormatException>();
        new Action(() => PendingCodec.Decode(With(Valid(), "plan", new JsonStringValue("x")))).Should().Throw<PendingFormatException>();
    }

    [Fact]
    public void Malformed_config_and_plan_entries_are_rejected()
    {
        new Action(() => PendingCodec.Decode(With(Valid(), "config", new JsonNumberValue("0")))).Should().Throw<PendingFormatException>();
        new Action(() => PendingCodec.Decode(WithConfigField("maxBytes", new JsonNumberValue("-1")))).Should().Throw<PendingFormatException>();
        new Action(() => PendingCodec.Decode(WithConfigField("exclude", new JsonStringValue("a")))).Should().Throw<PendingFormatException>();
        new Action(() => PendingCodec.Decode(With(Valid(), "plan", JsonParser.Parse("[1]")))).Should().Throw<PendingFormatException>();
        new Action(() => PendingCodec.Decode(With(Valid(), "plan", JsonParser.Parse("[{\"path\":\"a\",\"before\":1}]")))).Should().Throw<PendingFormatException>();
    }

    private static JsonObject Valid()
    {
        var transaction = new PendingTransaction(HistoryVersion.Current, 7, 3, 1, true, 100, new List<string>(), new List<PendingPlanEntry>());
        return (JsonObject)PendingCodec.Encode(transaction);
    }

    private static JsonValue With(JsonObject source, string name, JsonValue value)
    {
        var copy = new JsonObject();
        foreach (var member in source.Members)
            copy.Members.Add(new JsonMember(member.Name, string.Equals(member.Name, name, StringComparison.Ordinal) ? value : member.Value));
        return copy;
    }

    private static JsonValue WithConfigField(string name, JsonValue value)
    {
        var valid = Valid();
        var config = Required(valid, "config");
        var newConfig = new JsonObject();
        foreach (var member in ((JsonObject)config).Members)
            newConfig.Members.Add(new JsonMember(member.Name, string.Equals(member.Name, name, StringComparison.Ordinal) ? value : member.Value));
        return With(valid, "config", newConfig);
    }

    private static JsonValue Required(JsonObject obj, string name)
    {
        foreach (var member in obj.Members)
            if (string.Equals(member.Name, name, StringComparison.Ordinal))
                return member.Value;
        throw new InvalidOperationException("missing field " + name);
    }
}
