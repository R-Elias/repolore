using RepoLore.Core.Health;
using RepoLore.Core.Json;
using RepoLore.Core.Matching;
using RepoLore.Infrastructure.Health;

namespace RepoLore.Cli.Rendering;

public static class HealthRenderer
{
    public static void Render(HealthReport report, bool explainRequested, bool json, bool quiet)
    {
        if (json)
        {
            var root = new JsonObject();
            root.Members.Add(new JsonMember("healthy", new JsonBooleanValue(report.Findings.Count == 0)));
            root.Members.Add(new JsonMember("findings", Renderer.FindingsArray(report.Findings.ToList())));
            root.Members.Add(new JsonMember("explain", report.Explanation is null ? new JsonNullValue() : ExplainObject(report.Explanation)));
            Console.WriteLine(JsonWriter.Write(root));
            return;
        }

        if (quiet)
            return;

        foreach (var finding in report.Findings)
            Console.WriteLine($"{finding.Id} {finding.Path}: {finding.Message}");

        if (explainRequested)
            WriteExplanationText(report.Explanation);

        if (report.Findings.Count == 0)
            Console.WriteLine("healthy: no findings");
    }

    private static void WriteExplanationText(PolicyExplanation? explanation)
    {
        if (explanation is null)
        {
            Console.WriteLine("explain: not evaluated because a policy file is invalid; see the findings above");
            return;
        }

        var discovery = explanation.SourceDiscovery;
        Console.WriteLine($"explain {explanation.Path}{(explanation.IsDirectory ? "/" : "")}");
        Console.WriteLine($"  source discovery: {(discovery.Excluded ? "excluded" : "included")} ({Describe(discovery.Layer, discovery.Rule)})");
        Console.WriteLine($"  history coverage: {(explanation.HistoryCovered ? "covered" : "not covered")} ({explanation.HistoryReason}{RuleSuffix(explanation.HistoryRule)})");
        Console.WriteLine("  git tracking: not evaluated; ask Git (for example 'git check-ignore -v <path>')");
    }

    private static string Describe(string layer, CompiledRule? rule) =>
        rule is null ? "no rule matches" : $"{layer} rule '{rule.Display}'";

    private static string RuleSuffix(CompiledRule? rule) =>
        rule is null ? "" : $": '{rule.Display}'";

    private static JsonObject ExplainObject(PolicyExplanation explanation)
    {
        var discovery = new JsonObject();
        discovery.Members.Add(new JsonMember("excluded", new JsonBooleanValue(explanation.SourceDiscovery.Excluded)));
        discovery.Members.Add(new JsonMember("layer", new JsonStringValue(explanation.SourceDiscovery.Layer)));
        discovery.Members.Add(new JsonMember("rule", RuleSource(explanation.SourceDiscovery.Rule)));

        var history = new JsonObject();
        history.Members.Add(new JsonMember("covered", new JsonBooleanValue(explanation.HistoryCovered)));
        history.Members.Add(new JsonMember("reason", new JsonStringValue(explanation.HistoryReason)));
        history.Members.Add(new JsonMember("rule", RuleSource(explanation.HistoryRule)));

        var root = new JsonObject();
        root.Members.Add(new JsonMember("path", new JsonStringValue(explanation.Path)));
        root.Members.Add(new JsonMember("isDirectory", new JsonBooleanValue(explanation.IsDirectory)));
        root.Members.Add(new JsonMember("sourceDiscovery", discovery));
        root.Members.Add(new JsonMember("historyCoverage", history));
        root.Members.Add(new JsonMember("gitTracking", new JsonStringValue("not evaluated")));
        return root;
    }

    private static JsonValue RuleSource(CompiledRule? rule) =>
        rule is null ? new JsonNullValue() : new JsonStringValue(rule.Display);
}
