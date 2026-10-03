using FluentAssertions;
using RepoLore.Core.Mapping;
using Xunit;

namespace RepoLore.Core.Tests.Mapping;

public class KnowledgeMappingTests
{
    [Fact]
    public void Directory_mapping_matches_authored_expectations_and_round_trips()
    {
        var tsv = Path.Combine(AppContext.BaseDirectory, "Fixtures", "expected-mappings.tsv");
        var rows = File.ReadAllLines(tsv).Where(static l => l.Trim().Length > 0).ToArray();
        rows.Length.Should().BeGreaterThanOrEqualTo(3, $"expected at least 3 rows, got {rows.Length}");
        foreach (var line in rows)
        {
            var fields = line.Split('\t');
            fields.Length.Should().Be(2, line);
            var target = fields[0];
            var note = fields[1];
            KnowledgePathMapper.MapDirectoryNote(target).Should().Be(note, line);
            KnowledgePathMapper.TryDecode(note, out var decoded, out var finding).Should().BeTrue($"{line}: {finding}");
            decoded.Should().Be(target, line);
        }
    }

    [Fact]
    public void Repository_root_maps_to_the_fixed_root_md_name()
    {
        KnowledgePathMapper.MapDirectoryNote(".").Should().Be("root.md");
        KnowledgePathMapper.TryDecode("root.md", out var decoded, out var finding).Should().BeTrue(finding ?? "");
        decoded.Should().Be(".");
    }

    [Fact]
    public void Non_directory_note_paths_produce_findings_not_guessed_paths()
    {
        foreach (var bad in new[] { "src/client.cs.md", "foo/bar.md", "src.md", "root.md.txt", "src//src.md", "a.md/child.txt.md" })
        {
            KnowledgePathMapper.TryDecode(bad, out _, out var finding).Should().BeFalse($"expected rejection: {bad}");
            finding.Should().NotBeNullOrEmpty($"expected finding text: {bad}");
        }
    }
}
