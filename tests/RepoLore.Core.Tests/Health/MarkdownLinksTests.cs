using FluentAssertions;
using RepoLore.Core.Health;
using Xunit;

namespace RepoLore.Core.Tests.Health;

public class MarkdownLinksTests
{
    [Theory]
    [InlineData("[a](other.md)", "other.md")]
    [InlineData("[a](../x/y.md#section)", "../x/y.md")]
    [InlineData("![img](pic.png)", "pic.png")]
    [InlineData("[a](dir/ \"title\")", "dir/")]
    [InlineData("[a](<spaced name.md>)", "spaced name.md")]
    public void Relative_inline_file_links_are_extracted_with_fragments_stripped(string markdown, string expected)
    {
        MarkdownLinks.ExtractLocalTargets(markdown).Should().Equal(expected);
    }

    [Theory]
    [InlineData("[a](https://example.com/x.md)")]
    [InlineData("[a](http://example.com)")]
    [InlineData("[a](mailto:someone@example.com)")]
    [InlineData("[a](#only-fragment)")]
    [InlineData("[a](/absolute.md)")]
    [InlineData("[a]()")]
    [InlineData("no link here ](stray.md)")]
    public void Web_mail_fragment_absolute_and_malformed_links_are_skipped(string markdown)
    {
        MarkdownLinks.ExtractLocalTargets(markdown).Should().BeEmpty();
    }

    [Fact]
    public void Links_inside_code_spans_and_fences_are_skipped()
    {
        var markdown = "`[a](inline.md)` and [b](real.md)\n```\n[c](fenced.md)\n```\n~~~\n[d](tilde.md)\n~~~\n[e](after.md)\r\n";
        MarkdownLinks.ExtractLocalTargets(markdown).Should().Equal("real.md", "after.md");
    }

    [Fact]
    public void Duplicate_targets_are_reported_once()
    {
        MarkdownLinks.ExtractLocalTargets("[a](x.md) [b](x.md)").Should().Equal("x.md");
    }
}
