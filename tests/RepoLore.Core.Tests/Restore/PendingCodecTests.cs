using RepoLore.Core.Json;
using RepoLore.Core.Restore;
using RepoLore.Core.Snapshot;
using RepoLore.Core.Tests;

namespace RepoLore.Core.Tests.Restore;

public static class PendingCodecTests
{
    public static void Run()
    {
        TestRunner.Check("encode/decode round-trip preserves every field", () =>
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

            TestRunner.Equal(HistoryVersion.Current, decoded.HistoryVersion);
            TestRunner.Equal(7L, decoded.TargetId);
            TestRunner.Equal(3L, decoded.PreOperationId);
            TestRunner.Equal(1, decoded.FormatVersion);
            TestRunner.Equal(true, decoded.HistoryEnabled);
            TestRunner.Equal(1234L, decoded.MaxBytes);
            TestRunner.Equal(1, decoded.ExcludeRules.Count);
            TestRunner.Equal("_repolore/sessions/a/", decoded.ExcludeRules[0]);
            TestRunner.Equal(3, decoded.Plan.Count);
            TestRunner.Equal("_repolore/a.md", decoded.Plan[0].Path);
            TestRunner.Equal("oldHash", decoded.Plan[0].Before);
            TestRunner.Equal("newHash", decoded.Plan[0].After);
            TestRunner.Equal(null, decoded.Plan[1].Before);
            TestRunner.Equal(null, decoded.Plan[2].After);
        });

        TestRunner.Check("non-object and missing-field records are rejected", () =>
        {
            TestRunner.Throws<PendingFormatException>(() => PendingCodec.Decode(JsonParser.Parse("[1,2]")));
            TestRunner.Throws<PendingFormatException>(() => PendingCodec.Decode(JsonParser.Parse("{}")));
            TestRunner.Throws<PendingFormatException>(() => PendingCodec.Decode(JsonParser.Parse("{\"historyVersion\":1}")));
        });

        TestRunner.Check("wrong types and unknown versions are rejected", () =>
        {
            TestRunner.Throws<PendingFormatException>(() => PendingCodec.Decode(With(Valid(), "historyVersion", new JsonStringValue("1"))));
            TestRunner.Throws<PendingFormatException>(() => PendingCodec.Decode(With(Valid(), "historyVersion", new JsonNumberValue("2"))));
            TestRunner.Throws<PendingFormatException>(() => PendingCodec.Decode(With(Valid(), "targetId", new JsonStringValue("3"))));
            TestRunner.Throws<PendingFormatException>(() => PendingCodec.Decode(With(Valid(), "preOperationId", new JsonNumberValue("-1"))));
            TestRunner.Throws<PendingFormatException>(() => PendingCodec.Decode(With(Valid(), "plan", new JsonStringValue("x"))));
        });

        TestRunner.Check("malformed config and plan entries are rejected", () =>
        {
            TestRunner.Throws<PendingFormatException>(() => PendingCodec.Decode(With(Valid(), "config", new JsonNumberValue("0"))));
            TestRunner.Throws<PendingFormatException>(() => PendingCodec.Decode(WithConfigField("maxBytes", new JsonNumberValue("-1"))));
            TestRunner.Throws<PendingFormatException>(() => PendingCodec.Decode(WithConfigField("exclude", new JsonStringValue("a"))));
            TestRunner.Throws<PendingFormatException>(() => PendingCodec.Decode(With(Valid(), "plan", JsonParser.Parse("[1]"))));
            TestRunner.Throws<PendingFormatException>(() => PendingCodec.Decode(With(Valid(), "plan", JsonParser.Parse("[{\"path\":\"a\",\"before\":1}]"))));
        });
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
