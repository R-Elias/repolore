using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RepoLore.Core.Json;
using Xunit;

namespace RepoLore.Core.Tests.Json;

public class NewtonsoftParityTests
{
    private static readonly char[] StringAlphabet = "abc\"\\\n\t\u0001 é中".ToCharArray();
    private static readonly char[] KeyAlphabet = "abc\"\\ é".ToCharArray();

    [Fact]
    public void Parser_deeply_equals_newtonsoft_on_generated_inputs()
    {
        foreach (var node in Gen.Sample(NodeGen(6), 8, 200))
            VerifyParsedModel(node);
    }

    [Fact]
    public void Writer_round_trip_is_stable_and_newtonsoft_readable_on_generated_inputs()
    {
        foreach (var node in Gen.Sample(NodeGen(6), 8, 200))
            VerifyRoundTrip(node);
    }

    private static void VerifyParsedModel(JsonNode node)
    {
        var newtonsoft = NewtonsoftToken(node);
        var text = JsonConvert.SerializeObject(newtonsoft, Formatting.None);
        var parsed = JsonParser.Parse(text);
        JToken.DeepEquals(HandRolledToken(parsed), newtonsoft).Should().BeTrue(text);
    }

    private static void VerifyRoundTrip(JsonNode node)
    {
        var newtonsoft = NewtonsoftToken(node);
        var text = JsonConvert.SerializeObject(newtonsoft, Formatting.None);
        var written = RepoLore.Core.Json.JsonWriter.Write(JsonParser.Parse(text));
        var reparsed = JsonParser.Parse(written);
        JToken.DeepEquals(HandRolledToken(reparsed), newtonsoft).Should().BeTrue(written);
        JToken.DeepEquals(JToken.Parse(written), newtonsoft).Should().BeTrue(written);
    }

    private static Gen<JsonNode> NodeGen(int depth) =>
        depth <= 0
            ? LeafGen()
            : Gen.OneOf(new Gen<JsonNode>[] { LeafGen(), ObjectGen(depth - 1), ArrayGen(depth - 1) });

    private static Gen<JsonNode> LeafGen() =>
        Gen.OneOf(new Gen<JsonNode>[]
        {
            Gen.Select(Gen.Choose(-1_000_000, 1_000_000), i => (JsonNode)new JsonNumberNode(i)),
            Gen.Select(StringGen(), s => (JsonNode)new JsonStringNode(s)),
            Gen.Select(Gen.Elements(new[] { true, false }), b => (JsonNode)new JsonBoolNode(b)),
            Gen.Constant((JsonNode)new JsonNullNode())
        });

    private static Gen<JsonNode> ObjectGen(int depth) =>
        Gen.SelectMany(
            Gen.Choose(0, 4),
            count => Gen.Select(
                Gen.ListOf(MemberGen(depth), count),
                members => (JsonNode)new JsonObjectNode(EnsureUnique(members))));

    private static Gen<JsonNode> ArrayGen(int depth) =>
        Gen.SelectMany(
            Gen.Choose(0, 4),
            count => Gen.Select(
                Gen.ListOf(NodeGen(depth), count),
                items => (JsonNode)new JsonArrayNode(items)));

    private static Gen<JsonMemberNode> MemberGen(int depth) =>
        Gen.SelectMany(
            NameGen(),
            name => Gen.Select(NodeGen(depth), value => new JsonMemberNode(name, value)));

    private static Gen<string> NameGen() =>
        Gen.SelectMany(
            Gen.Choose(1, 6),
            length => Gen.Select(
                Gen.ListOf(Gen.Elements(KeyAlphabet), length),
                chars => new string(chars.ToArray())));

    private static Gen<string> StringGen() =>
        Gen.SelectMany(
            Gen.Choose(0, 15),
            length => Gen.Select(
                Gen.ListOf(Gen.Elements(StringAlphabet), length),
                chars => new string(chars.ToArray())));

    private static List<JsonMemberNode> EnsureUnique(IEnumerable<JsonMemberNode> members)
    {
        var result = new List<JsonMemberNode>();
        var index = 0;
        foreach (var member in members)
            result.Add(new JsonMemberNode(member.Name + index++, member.Value));
        return result;
    }

    private static JToken NewtonsoftToken(JsonNode node) => node switch
    {
        JsonObjectNode o => new JObject(o.Members.Select(m => new JProperty(m.Name, NewtonsoftToken(m.Value)))),
        JsonArrayNode a => new JArray(a.Items.Select(NewtonsoftToken)),
        JsonStringNode s => new JValue(s.Value),
        JsonNumberNode n => JToken.Parse(n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        JsonBoolNode b => new JValue(b.Value),
        JsonNullNode => JValue.CreateNull(),
        _ => throw new InvalidOperationException("Unknown JSON node type.")
    };

    private static JToken HandRolledToken(JsonValue value) => value switch
    {
        JsonObject o => new JObject(o.Members.Select(m => new JProperty(m.Name, HandRolledToken(m.Value)))),
        JsonArray a => new JArray(a.Items.Select(HandRolledToken)),
        JsonStringValue s => new JValue(s.Value),
        JsonNumberValue n => JToken.Parse(n.Raw),
        JsonBooleanValue b => new JValue(b.Value),
        JsonNullValue => JValue.CreateNull(),
        _ => throw new InvalidOperationException("Unknown JSON value type.")
    };

    private abstract class JsonNode { }

    private sealed class JsonObjectNode : JsonNode
    {
        public JsonObjectNode(List<JsonMemberNode> members) { Members = members; }
        public List<JsonMemberNode> Members { get; }
    }

    private sealed class JsonMemberNode
    {
        public JsonMemberNode(string name, JsonNode value) { Name = name; Value = value; }
        public string Name { get; }
        public JsonNode Value { get; }
    }

    private sealed class JsonArrayNode : JsonNode
    {
        public JsonArrayNode(IEnumerable<JsonNode> items) { Items = items.ToList(); }
        public List<JsonNode> Items { get; }
    }

    private sealed class JsonStringNode : JsonNode
    {
        public JsonStringNode(string value) { Value = value; }
        public string Value { get; }
    }

    private sealed class JsonNumberNode : JsonNode
    {
        public JsonNumberNode(long value) { Value = value; }
        public long Value { get; }
    }

    private sealed class JsonBoolNode : JsonNode
    {
        public JsonBoolNode(bool value) { Value = value; }
        public bool Value { get; }
    }

    private sealed class JsonNullNode : JsonNode { }
}
