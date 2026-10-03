using FluentAssertions;
using Xunit;

namespace RepoLore.Cli.Tests;

public class ExecutableFoundationTests
{
    [Fact]
    public void Version_outside_repository_is_read_only()
    {
        TestSupport.WithTemp(temp =>
        {
            File.WriteAllText(Path.Combine(temp, "sentinel.txt"), "OUTSIDE_REPOSITORY_SENTINEL\r\n");
            var before = TestSupport.Snapshot(temp);
            var result = TestSupport.Run(temp, TestSupport.Cli, "version");
            result.Code.Should().Be(0);
            TestSupport.Normalize(result.Out).Should().Be("RepoLore 0.1.0-preview.1\nSupported knowledge formats: 1\n");
            result.Error.Should().Be("");
            TestSupport.Snapshot(temp).Should().Be(before);
        });
    }

    [Fact]
    public void Named_fixtures_and_rejected_commands_remain_unchanged()
    {
        var names = new[] { "minimal-v1", "two-sessions", "alpha-sparse-only", "alpha-local-only", "alpha-conflict", "history-failures" };
        foreach (var name in names)
        {
            TestSupport.WithTemp(temp =>
            {
                TestSupport.CopyTree(Path.Combine(AppContext.BaseDirectory, "Fixtures", name), temp);
                File.ReadAllText(Path.Combine(temp, "unrelated", "sentinel.txt")).Should().Contain("SENTINEL");
                if (name == "two-sessions")
                {
                    foreach (var session in new[] { "a", "b" })
                        TestSupport.Normalize(File.ReadAllText(Path.Combine(temp, "_repolore", "sessions", "session-" + session, "root.md")))
                            .Should().Be($"SESSION_{session.ToUpperInvariant()}_ROOT_SENTINEL\n");
                    File.Exists(Path.Combine(temp, ".gitignore")).Should().BeTrue();
                }
                var before = TestSupport.Snapshot(temp);
                TestSupport.Run(temp, TestSupport.Cli, "version").Code.Should().Be(0);
                TestSupport.Run(temp, TestSupport.Cli, "--help").Code.Should().Be(0);
                foreach (var arguments in new[] { Array.Empty<string>(), new[] { "version", "--unknown" }, new[] { "version", "version" } })
                {
                    var result = TestSupport.Run(temp, new[] { TestSupport.Cli }.Concat(arguments).ToArray());
                    result.Code.Should().Be(2);
                    result.Out.Should().Be("");
                    result.Error.Should().StartWith("Usage:");
                }
                TestSupport.Snapshot(temp).Should().Be(before);
            });
        }
    }

    [Fact]
    public void Runtime_dependency_list_contains_exactly_the_three_shipped_projects()
    {
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(TestSupport.Cli, ".deps.json")));
        var libraries = json.RootElement.GetProperty("libraries").EnumerateObject().ToArray();
        string.Join(',', libraries.Select(x => x.Name.Split('/')[0]).Order(StringComparer.Ordinal))
            .Should().Be("RepoLore.Cli,RepoLore.Core,RepoLore.Infrastructure");
        libraries.All(x => x.Value.GetProperty("type").GetString() == "project").Should().BeTrue();
    }

    [Fact]
    public void Build_guards_reject_forbidden_capabilities_and_recover_after_removal()
    {
        TestSupport.WithTemp(temp =>
        {
            foreach (var file in new[] { "global.json", "Directory.Build.props", "Directory.Build.targets", "NuGet.Config" })
                File.Copy(Path.Combine(TestSupport.Repo, file), Path.Combine(temp, file));
            TestSupport.CopyTree(Path.Combine(TestSupport.Repo, "build"), Path.Combine(temp, "build"));
            TestSupport.CopyTree(Path.Combine(TestSupport.Repo, "src"), Path.Combine(temp, "src"));
            var project = Path.Combine(temp, "src", "RepoLore.Cli", "RepoLore.Cli.csproj");
            var baseline = TestSupport.Run(temp, "build", project, "--configuration", "Release", "--nologo");
            baseline.Code.Should().Be(0);
            var probes = new (string Project, string Code)[]
            {
                ("Core", "public static object Run() => new System.Net.Http.HttpClient();"),
                ("Infrastructure", "public static object Run() => System.Net.Dns.GetHostAddresses(\"localhost\");"),
                ("Cli", "public static object Run() => new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);"),
                ("Core", "public static object? Run() => System.Diagnostics.Process.Start(\"echo\");"),
                ("Infrastructure", "public static object Run() => System.Reflection.Assembly.Load(\"example\");"),
                ("Cli", "public static object Run() => System.Runtime.Loader.AssemblyLoadContext.Default;"),
                ("Core", "[System.Runtime.InteropServices.DllImport(\"example\")] public static extern void Run();"),
                ("Infrastructure", "public static nint Run() => System.Runtime.InteropServices.NativeLibrary.Load(\"example\");"),
                ("Cli", "public static object? Run() => System.Type.GetType(\"example\");")
            };
            foreach (var probe in probes)
            {
                var path = Path.Combine(temp, "src", "RepoLore." + probe.Project, "ForbiddenProbe.cs");
                File.WriteAllText(path, "public static class ForbiddenProbe { " + probe.Code + " }");
                try
                {
                    var result = TestSupport.Run(temp, "build", project, "--configuration", "Release", "--no-restore", "--nologo");
                    result.Code.Should().NotBe(0);
                    result.Out.Should().Contain("RLBOUNDARY:");
                }
                finally { File.Delete(path); }
            }
            var originalProject = File.ReadAllText(project);
            var dependencyProbe = System.Xml.Linq.XDocument.Parse(originalProject);
            dependencyProbe.Root!.Add(new System.Xml.Linq.XElement("ItemGroup", new System.Xml.Linq.XElement("Reference",
                new System.Xml.Linq.XAttribute("Include", "ForbiddenDependency"),
                new System.Xml.Linq.XElement("HintPath", typeof(ExecutableFoundationTests).Assembly.Location))));
            try
            {
                dependencyProbe.Save(project);
                var result = TestSupport.Run(temp, "build", project, "--configuration", "Release", "--no-restore", "--nologo");
                result.Code.Should().NotBe(0);
                result.Out.Should().Contain("RLBOUNDARY: non-BCL runtime references:");
            }
            finally { File.WriteAllText(project, originalProject); }
            var recovered = TestSupport.Run(temp, "build", project, "--configuration", "Release", "--no-restore", "--nologo");
            recovered.Code.Should().Be(0);
        });
    }
}
