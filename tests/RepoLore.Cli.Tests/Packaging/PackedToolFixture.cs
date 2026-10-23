using System.IO.Compression;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace RepoLore.Cli.Tests.Packaging;

[CollectionDefinition(Name)]
public sealed class PackagingCollection : ICollectionFixture<PackedToolFixture>
{
    public const string Name = "Packed tool";
}

public sealed class PackedToolFixture : IDisposable
{
    private static readonly string[] ShippedProjects =
    [
        "src/RepoLore.Core/RepoLore.Core.csproj",
        "src/RepoLore.Infrastructure/RepoLore.Infrastructure.csproj",
        "src/RepoLore.Cli/RepoLore.Cli.csproj",
    ];

    public string FeedDirectory { get; } = Path.Combine(Path.GetTempPath(), "repolore-feed-" + Guid.NewGuid().ToString("N"));
    public IReadOnlyList<string> PackagePaths { get; }
    public string PackagePath => PackagePaths.Single();
    public string PackageId => NuspecValue("id");
    public string PackageVersion => NuspecValue("version");
    public string SourceVersion { get; } = ReadSourceVersion();

    public PackedToolFixture()
    {
        Directory.CreateDirectory(FeedDirectory);
        foreach (var project in ShippedProjects)
        {
            var (code, output, error) = TestSupport.Run(TestSupport.Repo, "pack", project,
                "--configuration", "Release", "--no-restore", "--disable-build-servers", "--output", FeedDirectory);
            code.Should().Be(0, $"packing {project} must succeed:\n{output}\n{error}");
        }
        PackagePaths = Directory.GetFiles(FeedDirectory, "*.nupkg");
    }

    public IReadOnlyList<string> EntryNames()
    {
        using var archive = ZipFile.OpenRead(PackagePath);
        return archive.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal).ToArray();
    }

    public byte[] EntryBytes(string name)
    {
        using var archive = ZipFile.OpenRead(PackagePath);
        using var stream = archive.GetEntry(name)!.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    public string EntryText(string name) => System.Text.Encoding.UTF8.GetString(EntryBytes(name)).TrimStart('﻿');

    public XDocument Nuspec() => XDocument.Parse(EntryText(EntryNames().Single(name => name.EndsWith(".nuspec", StringComparison.Ordinal))));

    public void Dispose() => Directory.Delete(FeedDirectory, recursive: true);

    private string NuspecValue(string element) => Nuspec().Descendants().First(node => node.Name.LocalName == element).Value;

    private static string ReadSourceVersion() =>
        XDocument.Load(Path.Combine(TestSupport.Repo, "Directory.Build.props")).Descendants("Version").Single().Value;
}
