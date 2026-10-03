using System.Text;
using FluentAssertions;
using RepoLore.Core.Context;
using Xunit;

namespace RepoLore.Core.Tests.Context;

public class NoteReadTests
{
    [Fact]
    public void Normalization_removes_bom_normalizes_crlf_trims_whitespace()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var body = Encoding.UTF8.GetBytes("  hello\r\nworld\r\n");
        var bytes = bom.Concat(body).ToArray();
        NoteContent.TryNormalize(bytes, out var text, out _).Should().BeTrue();
        text.Should().Be("hello\nworld");
    }

    [Fact]
    public void Invalid_utf8_is_a_finding_not_replacement_text()
    {
        var bytes = new byte[] { 0xFF, 0xFE, 0xFD };
        NoteContent.TryNormalize(bytes, out _, out var finding).Should().BeFalse();
        finding.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Empty_marker_is_treated_as_empty_content()
    {
        NoteContent.IsEmpty("<!-- repolore:empty -->").Should().BeTrue();
        NoteContent.IsEmpty("").Should().BeTrue();
        NoteContent.IsEmpty("actual text").Should().BeFalse();
    }

    [Fact]
    public void Alpha_resolver_prefers_sparse_falls_back_to_tree_reports_conflicts()
    {
        var sparseOnly = NoteReadResolver.Resolve(NoteFileState.Content("X"), NoteFileState.Absent);
        sparseOnly.Status.Should().Be(NoteStatus.Found);
        sparseOnly.IsLegacy.Should().BeFalse();
        sparseOnly.Text.Should().Be("X");

        var legacyOnly = NoteReadResolver.Resolve(NoteFileState.Absent, NoteFileState.Content("Y"));
        legacyOnly.Status.Should().Be(NoteStatus.Found);
        legacyOnly.IsLegacy.Should().BeTrue();
        legacyOnly.Text.Should().Be("Y");

        var same = NoteReadResolver.Resolve(NoteFileState.Content("Z"), NoteFileState.Content("Z"));
        same.Status.Should().Be(NoteStatus.Found);
        same.IsLegacy.Should().BeFalse();

        var conflict = NoteReadResolver.Resolve(NoteFileState.Content("A"), NoteFileState.Content("B"));
        conflict.Status.Should().Be(NoteStatus.Conflict);

        var bothEmpty = NoteReadResolver.Resolve(NoteFileState.Empty(), NoteFileState.Empty());
        bothEmpty.Status.Should().Be(NoteStatus.Empty);

        var sparseEmptyLegacyContent = NoteReadResolver.Resolve(NoteFileState.Empty(), NoteFileState.Content("W"));
        sparseEmptyLegacyContent.Status.Should().Be(NoteStatus.Found);
        sparseEmptyLegacyContent.IsLegacy.Should().BeTrue();

        var invalid = NoteReadResolver.Resolve(NoteFileState.InvalidUtf8(), NoteFileState.Absent);
        invalid.Status.Should().Be(NoteStatus.InvalidUtf8);

        var missing = NoteReadResolver.Resolve(NoteFileState.Absent, NoteFileState.Absent);
        missing.Status.Should().Be(NoteStatus.Missing);
    }
}
