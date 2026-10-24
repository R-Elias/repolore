using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace RepoLore.Cli.Tests.Packaging;

[Collection(PackagingCollection.Name)]
public sealed class PackageContentsTests(PackedToolFixture package)
{
    private const string ToolFolder = "tools/net10.0/any/";

    private static readonly string[] AllowedLibraries = ["RepoLore.Cli", "RepoLore.Core", "RepoLore.Infrastructure"];

    [Fact]
    public void Packing_all_shipped_projects_produces_exactly_one_package_named_RepoLore_Cli()
    {
        package.PackagePaths.Select(Path.GetFileName).Should().Equal($"RepoLore.Cli.{package.SourceVersion}.nupkg");
        package.PackageId.Should().Be("RepoLore.Cli");
    }

    [Fact]
    public void Package_version_comes_from_the_single_version_source()
    {
        package.PackageVersion.Should().Be(package.SourceVersion);
    }

    [Fact]
    public void Package_is_a_dotnet_tool_with_the_repolore_command_and_no_package_dependencies()
    {
        var nuspec = package.Nuspec();
        nuspec.Descendants().Where(node => node.Name.LocalName == "packageType").Select(node => node.Attribute("name")!.Value)
            .Should().Equal("DotnetTool");
        nuspec.Descendants().Where(node => node.Name.LocalName is "dependencies" or "dependency").Should().BeEmpty();

        var settings = System.Xml.Linq.XDocument.Parse(package.EntryText(ToolFolder + "DotnetToolSettings.xml"));
        settings.Descendants("Command").Select(node => node.Attribute("Name")!.Value).Should().Equal("repolore");
    }

    [Fact]
    public void Package_metadata_is_complete_and_not_the_sdk_placeholder()
    {
        var values = package.Nuspec().Descendants().Where(node => node.Name.LocalName is "authors" or "copyright" or "description" or "license" or "projectUrl")
            .ToDictionary(node => node.Name.LocalName, node => node.Value);
        values["license"].Should().Be("Apache-2.0");
        values["description"].Should().NotBe("Package Description").And.Contain("RepoLore");
        values["authors"].Should().Be("Elias Aliche");
        values["copyright"].Should().Be("Copyright 2026 Elias Aliche");
        values["projectUrl"].Should().StartWith("https://");
    }

    [Fact]
    public void Package_readme_is_the_repository_readme_and_states_the_signature_limitation()
    {
        package.Nuspec().Descendants().Single(node => node.Name.LocalName == "readme").Value.Should().Be("README.md");

        var readme = package.EntryText("README.md");
        readme.Should().Be(File.ReadAllText(Path.Combine(TestSupport.Repo, "README.md")));
        readme.Should().Contain("no author signature").And.Contain("not an enterprise-ready or certified release");
    }

    [Fact]
    public void Package_contains_only_the_three_shipped_assemblies_and_their_tool_files()
    {
        var expected = new[]
        {
            "[Content_Types].xml",
            "_rels/.rels",
            "README.md",
            "RepoLore.Cli.nuspec",
            ToolFolder + "DotnetToolSettings.xml",
            ToolFolder + "RepoLore.Cli.deps.json",
            ToolFolder + "RepoLore.Cli.dll",
            ToolFolder + "RepoLore.Cli.pdb",
            ToolFolder + "RepoLore.Cli.runtimeconfig.json",
            ToolFolder + "RepoLore.Core.dll",
            ToolFolder + "RepoLore.Core.pdb",
            ToolFolder + "RepoLore.Infrastructure.dll",
            ToolFolder + "RepoLore.Infrastructure.pdb",
        };
        package.EntryNames().Where(name => !name.StartsWith("package/services/metadata/", StringComparison.Ordinal))
            .Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Package_carries_no_knowledge_sessions_history_test_assets_or_test_stack()
    {
        var forbiddenFragments = new[]
        {
            "_repolore", "sessions", ".history", "repolore.json", "Fixtures", ".gitignore", "NuGet.Config",
            "xunit", "FluentAssertions", "FsCheck", "CliWrap", "Newtonsoft", "Microsoft.TestPlatform", "Microsoft.NET.Test", "testhost",
        };
        var offending = package.EntryNames().Where(name => forbiddenFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
        offending.Should().BeEmpty();
    }

    [Fact]
    public void Package_files_contain_no_build_machine_paths()
    {
        var machinePaths = new[] { TestSupport.Repo, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) }
            .SelectMany(path => new[] { path, path.Replace('\\', '/') })
            .Where(path => path.Length > 3)
            .Distinct()
            .ToArray();
        foreach (var entry in package.EntryNames())
        {
            var bytes = package.EntryBytes(entry);
            foreach (var machinePath in machinePaths)
                bytes.AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes(machinePath)).Should().Be(-1, $"{entry} must not embed '{machinePath}'");
        }
    }

    [Fact]
    public void Runtime_dependency_list_inside_the_package_is_the_three_project_libraries()
    {
        using var json = JsonDocument.Parse(package.EntryText(ToolFolder + "RepoLore.Cli.deps.json"));
        var libraries = json.RootElement.GetProperty("libraries").EnumerateObject().ToArray();
        libraries.Select(library => library.Name.Split('/')[0]).Order(StringComparer.Ordinal).Should().Equal(AllowedLibraries);
        libraries.Should().OnlyContain(library => library.Value.GetProperty("type").GetString() == "project");
    }

    [Fact]
    public void Runtime_config_targets_net10_and_refuses_major_runtime_roll_forward()
    {
        using var json = JsonDocument.Parse(package.EntryText(ToolFolder + "RepoLore.Cli.runtimeconfig.json"));
        var options = json.RootElement.GetProperty("runtimeOptions");
        options.GetProperty("tfm").GetString().Should().Be("net10.0");
        options.GetProperty("framework").GetProperty("name").GetString().Should().Be("Microsoft.NETCore.App");
        options.GetProperty("framework").GetProperty("version").GetString().Should().StartWith("10.");
        options.GetProperty("rollForward").GetString().Should().Be("LatestPatch");
    }
}
