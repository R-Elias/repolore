using RepoLore.Core;

namespace RepoLore.Core.Tests;

public static class KnowledgeMappingTests
{
    public static void Run()
    {
        TestRunner.Check("directory mapping matches authored expectations and round-trips", () =>
        {
            var tsv = Path.Combine(AppContext.BaseDirectory, "Fixtures", "expected-mappings.tsv");
            var rows = File.ReadAllLines(tsv).Where(static l => l.Trim().Length > 0).ToArray();
            TestRunner.True(rows.Length >= 3, $"expected at least 3 rows, got {rows.Length}");
            foreach (var line in rows)
            {
                var fields = line.Split('\t');
                TestRunner.Equal(2, fields.Length, line);
                var target = fields[0];
                var note = fields[1];
                TestRunner.Equal(note, KnowledgePathMapper.MapDirectoryNote(target), line);
                TestRunner.True(KnowledgePathMapper.TryDecode(note, out var decoded, out var finding), $"{line}: {finding}");
                TestRunner.Equal(target, decoded, line);
            }
        });

        TestRunner.Check("repository root maps to the fixed root.md name", () =>
        {
            TestRunner.Equal("root.md", KnowledgePathMapper.MapDirectoryNote("."));
            TestRunner.True(KnowledgePathMapper.TryDecode("root.md", out var decoded, out var finding), finding ?? "");
            TestRunner.Equal(".", decoded);
        });

        TestRunner.Check("non-directory note paths produce findings, not guessed paths", () =>
        {
            foreach (var bad in new[] { "src/client.cs.md", "foo/bar.md", "src.md", "root.md.txt", "src//src.md", "a.md/child.txt.md" })
            {
                TestRunner.True(!KnowledgePathMapper.TryDecode(bad, out _, out var finding), $"expected rejection: {bad}");
                TestRunner.True(!string.IsNullOrEmpty(finding), $"expected finding text: {bad}");
            }
        });
    }
}
