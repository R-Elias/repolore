using System.Security.Cryptography;
using FluentAssertions;

namespace RepoLore.Cli.Tests.Packaging.Journey;

public sealed class JourneyFile(string relativePath, string text, string expectedSha256)
{
    public string RelativePath { get; } = relativePath;
    public string Text { get; } = text;
    public string ExpectedSha256 { get; } = expectedSha256;

    public string FullPath(string repository) => Path.Combine(repository, RelativePath);

    public void WriteTo(string repository)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FullPath(repository))!);
        File.WriteAllText(FullPath(repository), Text);
        AssertHashIn(repository);
    }

    public void AssertHashIn(string repository) => HashOf(FullPath(repository)).Should().Be(ExpectedSha256, RelativePath);

    public static string HashOf(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
