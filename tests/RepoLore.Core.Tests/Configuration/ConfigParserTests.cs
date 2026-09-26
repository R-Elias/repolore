using RepoLore.Core.Configuration;
using RepoLore.Core.Json;
using RepoLore.Core.Tests;

namespace RepoLore.Core.Tests.Configuration;

public static class ConfigParserTests
{
    public static void Run()
    {
        TestRunner.Check("minimal config applies documented defaults", () =>
        {
            var config = ConfigParser.Parse("{\"formatVersion\":1}");
            TestRunner.Equal(1, config.FormatVersion);
            TestRunner.Equal(true, config.HistoryEnabled);
            TestRunner.Equal(209715200L, config.HistoryMaxBytes);
            TestRunner.Equal(0, config.HistoryExclude.Rules.Count);
        });

        TestRunner.Check("full config parses every field", () =>
        {
            var json = "{\"formatVersion\":1,\"history\":{\"enabled\":false,\"maxBytes\":1024,\"exclude\":[\"_repolore/sessions/a/\"]}}";
            var config = ConfigParser.Parse(json);
            TestRunner.Equal(false, config.HistoryEnabled);
            TestRunner.Equal(1024L, config.HistoryMaxBytes);
            TestRunner.Equal(1, config.HistoryExclude.Rules.Count);
        });

        TestRunner.Check("malformed JSON is an error, never a silent fallback", () =>
        {
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("{formatVersion:1}"));
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse(""));
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("[1,2]"));
        });

        TestRunner.Check("duplicate known keys are rejected", () =>
        {
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("{\"formatVersion\":1,\"formatVersion\":2}"));
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{},\"history\":{}}"));
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"enabled\":true,\"enabled\":false}}"));
        });

        TestRunner.Check("wrong types are rejected", () =>
        {
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("{\"formatVersion\":\"1\"}"));
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("{\"formatVersion\":true}"));
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":\"yes\"}"));
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"enabled\":\"yes\"}}"));
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"maxBytes\":\"big\"}}"));
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"exclude\":\"a\"}}"));
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"exclude\":[1]}}"));
        });

        TestRunner.Check("negative, non-integral, and overflowing budgets are rejected", () =>
        {
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"maxBytes\":-1}}"));
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"maxBytes\":1.5}}"));
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"maxBytes\":1e10}}"));
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"maxBytes\":9223372036854775808}}"));
        });

        TestRunner.Check("invalid history.exclude rules are rejected before use", () =>
        {
            var ex = TestRunner.Capture<ConfigException>(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"exclude\":[\"ab**cd\"]}}"));
            TestRunner.True(ex.Message.Contains("history.exclude", StringComparison.Ordinal), ex.Message);
        });

        TestRunner.Check("unknown format versions fail safely", () =>
        {
            var upgrade = TestRunner.Capture<ConfigException>(() => ConfigParser.Parse("{\"formatVersion\":2}"));
            TestRunner.True(upgrade.Message.Contains("Upgrade", StringComparison.Ordinal), upgrade.Message);
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("{\"formatVersion\":0}"));
        });

        TestRunner.Check("missing formatVersion is rejected", () =>
        {
            TestRunner.Throws<ConfigException>(() => ConfigParser.Parse("{\"history\":{}}"));
        });

        TestRunner.Check("unknown fields are preserved for explicit rewrites", () =>
        {
            var json = "{\"formatVersion\":1,\"custom\":{\"x\":[1,true,null],\"s\":\"v\"},\"history\":{\"enabled\":true,\"extra\":\"keep\"}}";
            var config = ConfigParser.Parse(json);
            var written = JsonWriter.Write(config.Raw);
            TestRunner.True(written.Contains("\"custom\":", StringComparison.Ordinal), written);
            TestRunner.True(written.Contains("\"x\":[1,true,null]", StringComparison.Ordinal), written);
            TestRunner.True(written.Contains("\"extra\":\"keep\"", StringComparison.Ordinal), written);
        });
    }
}
