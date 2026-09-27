using System.Text;
using RepoLore.Core.Context;
using RepoLore.Core.Tests;

namespace RepoLore.Core.Tests.Context;

public static class NoteReadTests
{
    public static void Run()
    {
        TestRunner.Check("normalization removes BOM, normalizes CRLF, trims whitespace", () =>
        {
            var bom = new byte[] { 0xEF, 0xBB, 0xBF };
            var body = Encoding.UTF8.GetBytes("  hello\r\nworld\r\n");
            var bytes = bom.Concat(body).ToArray();
            TestRunner.True(NoteContent.TryNormalize(bytes, out var text, out _));
            TestRunner.Equal("hello\nworld", text);
        });

        TestRunner.Check("invalid UTF-8 is a finding, not replacement text", () =>
        {
            var bytes = new byte[] { 0xFF, 0xFE, 0xFD };
            TestRunner.True(!NoteContent.TryNormalize(bytes, out _, out var finding));
            TestRunner.True(!string.IsNullOrEmpty(finding));
        });

        TestRunner.Check("empty marker is treated as empty content", () =>
        {
            TestRunner.True(NoteContent.IsEmpty("<!-- repolore:empty -->"));
            TestRunner.True(NoteContent.IsEmpty(""));
            TestRunner.True(!NoteContent.IsEmpty("actual text"));
        });

        TestRunner.Check("alpha resolver prefers sparse, falls back to tree, reports conflicts", () =>
        {
            var sparseOnly = NoteReadResolver.Resolve(NoteFileState.Content("X"), NoteFileState.Absent);
            TestRunner.Equal(NoteStatus.Found, sparseOnly.Status);
            TestRunner.Equal(false, sparseOnly.IsLegacy);
            TestRunner.Equal("X", sparseOnly.Text);

            var legacyOnly = NoteReadResolver.Resolve(NoteFileState.Absent, NoteFileState.Content("Y"));
            TestRunner.Equal(NoteStatus.Found, legacyOnly.Status);
            TestRunner.Equal(true, legacyOnly.IsLegacy);
            TestRunner.Equal("Y", legacyOnly.Text);

            var same = NoteReadResolver.Resolve(NoteFileState.Content("Z"), NoteFileState.Content("Z"));
            TestRunner.Equal(NoteStatus.Found, same.Status);
            TestRunner.Equal(false, same.IsLegacy);

            var conflict = NoteReadResolver.Resolve(NoteFileState.Content("A"), NoteFileState.Content("B"));
            TestRunner.Equal(NoteStatus.Conflict, conflict.Status);

            var bothEmpty = NoteReadResolver.Resolve(NoteFileState.Empty(), NoteFileState.Empty());
            TestRunner.Equal(NoteStatus.Empty, bothEmpty.Status);

            var sparseEmptyLegacyContent = NoteReadResolver.Resolve(NoteFileState.Empty(), NoteFileState.Content("W"));
            TestRunner.Equal(NoteStatus.Found, sparseEmptyLegacyContent.Status);
            TestRunner.Equal(true, sparseEmptyLegacyContent.IsLegacy);

            var invalid = NoteReadResolver.Resolve(NoteFileState.InvalidUtf8(), NoteFileState.Absent);
            TestRunner.Equal(NoteStatus.InvalidUtf8, invalid.Status);

            var missing = NoteReadResolver.Resolve(NoteFileState.Absent, NoteFileState.Absent);
            TestRunner.Equal(NoteStatus.Missing, missing.Status);
        });
    }
}
