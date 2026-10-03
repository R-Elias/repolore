using FluentAssertions;
using RepoLore.Core.Configuration;
using RepoLore.Core.Json;
using Xunit;

namespace RepoLore.Core.Tests.Configuration;

public class ConfigParserTests
{
    [Fact]
    public void Minimal_config_applies_documented_defaults()
    {
        var config = ConfigParser.Parse("{\"formatVersion\":1}");
        config.FormatVersion.Should().Be(1);
        config.HistoryEnabled.Should().BeTrue();
        config.HistoryMaxBytes.Should().Be(209715200L);
        config.HistoryExclude.Rules.Should().BeEmpty();
    }

    [Fact]
    public void Full_config_parses_every_field()
    {
        var json = "{\"formatVersion\":1,\"history\":{\"enabled\":false,\"maxBytes\":1024,\"exclude\":[\"_repolore/sessions/a/\"]}}";
        var config = ConfigParser.Parse(json);
        config.HistoryEnabled.Should().BeFalse();
        config.HistoryMaxBytes.Should().Be(1024L);
        config.HistoryExclude.Rules.Should().HaveCount(1);
    }

    [Fact]
    public void Malformed_json_is_an_error_never_a_silent_fallback()
    {
        new Action(() => ConfigParser.Parse("{formatVersion:1}")).Should().Throw<ConfigException>();
        new Action(() => ConfigParser.Parse("")).Should().Throw<ConfigException>();
        new Action(() => ConfigParser.Parse("[1,2]")).Should().Throw<ConfigException>();
    }

    [Fact]
    public void Duplicate_known_keys_are_rejected()
    {
        new Action(() => ConfigParser.Parse("{\"formatVersion\":1,\"formatVersion\":2}")).Should().Throw<ConfigException>();
        new Action(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{},\"history\":{}}")).Should().Throw<ConfigException>();
        new Action(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"enabled\":true,\"enabled\":false}}")).Should().Throw<ConfigException>();
    }

    [Fact]
    public void Wrong_types_are_rejected()
    {
        new Action(() => ConfigParser.Parse("{\"formatVersion\":\"1\"}")).Should().Throw<ConfigException>();
        new Action(() => ConfigParser.Parse("{\"formatVersion\":true}")).Should().Throw<ConfigException>();
        new Action(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":\"yes\"}")).Should().Throw<ConfigException>();
        new Action(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"enabled\":\"yes\"}}")).Should().Throw<ConfigException>();
        new Action(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"maxBytes\":\"big\"}}")).Should().Throw<ConfigException>();
        new Action(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"exclude\":\"a\"}}")).Should().Throw<ConfigException>();
        new Action(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"exclude\":[1]}}")).Should().Throw<ConfigException>();
    }

    [Fact]
    public void Negative_non_integral_and_overflowing_budgets_are_rejected()
    {
        new Action(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"maxBytes\":-1}}")).Should().Throw<ConfigException>();
        new Action(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"maxBytes\":1.5}}")).Should().Throw<ConfigException>();
        new Action(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"maxBytes\":1e10}}")).Should().Throw<ConfigException>();
        new Action(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"maxBytes\":9223372036854775808}}")).Should().Throw<ConfigException>();
    }

    [Fact]
    public void Invalid_history_exclude_rules_are_rejected_before_use()
    {
        var ex = new Action(() => ConfigParser.Parse("{\"formatVersion\":1,\"history\":{\"exclude\":[\"ab**cd\"]}}"))
            .Should().Throw<ConfigException>().Which;
        ex.Message.Should().Contain("history.exclude");
    }

    [Fact]
    public void Unknown_format_versions_fail_safely()
    {
        var upgrade = new Action(() => ConfigParser.Parse("{\"formatVersion\":2}"))
            .Should().Throw<ConfigException>().Which;
        upgrade.Message.Should().Contain("Upgrade");
        new Action(() => ConfigParser.Parse("{\"formatVersion\":0}")).Should().Throw<ConfigException>();
    }

    [Fact]
    public void Missing_formatVersion_is_rejected()
    {
        new Action(() => ConfigParser.Parse("{\"history\":{}}")).Should().Throw<ConfigException>();
    }

    [Fact]
    public void Unknown_fields_are_preserved_for_explicit_rewrites()
    {
        var json = "{\"formatVersion\":1,\"custom\":{\"x\":[1,true,null],\"s\":\"v\"},\"history\":{\"enabled\":true,\"extra\":\"keep\"}}";
        var config = ConfigParser.Parse(json);
        var written = JsonWriter.Write(config.Raw);
        written.Should().Contain("\"custom\":");
        written.Should().Contain("\"x\":[1,true,null]");
        written.Should().Contain("\"extra\":\"keep\"");
    }
}
