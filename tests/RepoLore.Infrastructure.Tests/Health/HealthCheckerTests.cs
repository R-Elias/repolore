using FluentAssertions;
using RepoLore.Core.Health;
using RepoLore.Core.Restore;
using RepoLore.Core.Snapshot;
using RepoLore.Infrastructure.Health;
using RepoLore.Infrastructure.History;
using Xunit;

namespace RepoLore.Infrastructure.Tests.Health;

public class HealthCheckerTests
{
    [Fact]
    public void A_healthy_knowledge_base_has_no_findings_and_no_history_is_normal()
    {
        WithKnowledgeBase(temp =>
        {
            Check(temp).Findings.Should().BeEmpty();
            Directory.Exists(Path.Combine(temp, "_repolore", ".history")).Should().BeFalse();
        });
    }

    [Fact]
    public void A_missing_optional_path_note_is_not_a_finding_but_a_missing_required_note_is()
    {
        WithKnowledgeBase(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "src", "unnoted"));
            Check(temp).Findings.Should().BeEmpty();

            File.Delete(Path.Combine(temp, "_repolore", "method.md"));
            IdsOf(Check(temp)).Should().Equal(FindingIds.MissingNote);
        });
    }

    [Fact]
    public void Health_check_writes_nothing()
    {
        WithKnowledgeBase(temp =>
        {
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "[gone](missing.md)");
            var before = Snapshot(temp);
            Check(temp).Findings.Should().NotBeEmpty();
            Snapshot(temp).Should().Be(before);
        });
    }

    [Fact]
    public void Invalid_utf8_notes_and_broken_local_links_are_reported_but_web_links_are_not()
    {
        WithKnowledgeBase(temp =>
        {
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "[web](https://example.com/x.md) [ok](method.md) [bad](nope.md) [up](../../escape.md)");
            File.WriteAllBytes(Path.Combine(temp, "_repolore", "sparse-tree", "src", "src.md"), new byte[] { 0xFF, 0xFE, 0x41 });

            var report = Check(temp);
            report.Findings.Where(f => f.Id == FindingIds.BrokenLink).Select(f => f.Message).Should().HaveCount(2);
            report.Findings.Should().ContainSingle(f => f.Id == FindingIds.InvalidUtf8 && f.Path == "_repolore/sparse-tree/src/src.md");
        });
    }

    [Fact]
    public void A_note_that_does_not_match_its_directory_has_an_invalid_name()
    {
        WithKnowledgeBase(temp =>
        {
            Write(temp, "_repolore/sparse-tree/src/other.md", "misplaced");
            IdsOf(Check(temp)).Should().Equal(FindingIds.InvalidNoteName);
        });
    }

    [Fact]
    public void A_note_for_a_missing_source_directory_is_an_orphan_but_an_excluded_source_directory_is_not()
    {
        WithKnowledgeBase(temp =>
        {
            Write(temp, "_repolore/sparse-tree/ghost/ghost.md", "knowledge");
            Write(temp, "_repolore/sparse-tree/build/build.md", "knowledge");
            Directory.CreateDirectory(Path.Combine(temp, "build"));

            Check(temp).Findings.Should().ContainSingle(f => f.Id == FindingIds.OrphanPathNote && f.Path == "_repolore/sparse-tree/ghost/ghost.md");
        });
    }

    [Fact]
    public void Differing_alpha_copies_conflict_and_a_marker_less_knowledge_base_needs_migration()
    {
        TempDir.WithTemp(temp =>
        {
            Write(temp, "_repolore/method.md", "m");
            Write(temp, "_repolore/root.md", "r");
            Write(temp, "_repolore/tree/src/src.md", "tree variant");
            Write(temp, "_repolore/sparse-tree/src/src.md", "sparse variant");
            Directory.CreateDirectory(Path.Combine(temp, "src"));

            IdsOf(Check(temp)).Should().BeEquivalentTo(FindingIds.ConflictingAlphaNotes, FindingIds.MigrationNeeded);
        });
    }

    [Theory]
    [InlineData("{broken", FindingIds.MalformedFormat)]
    [InlineData("{\"formatVersion\":2}", FindingIds.UnsupportedFormat)]
    [InlineData("{\"formatVersion\":1,\"history\":{\"maxBytes\":-5}}", FindingIds.PolicyError)]
    public void Configuration_problems_are_distinguished(string config, string expectedId)
    {
        WithKnowledgeBase(temp =>
        {
            Write(temp, "_repolore/repolore.json", config);
            IdsOf(Check(temp)).Should().Equal(expectedId);
        });
    }

    [Fact]
    public void An_invalid_ignore_rule_is_a_policy_error_with_its_line_number()
    {
        WithKnowledgeBase(temp =>
        {
            Write(temp, "_repoloreignore", "# comment\nab**cd\n");
            Check(temp).Findings.Should().ContainSingle(f => f.Id == FindingIds.PolicyError && f.Path == "_repoloreignore" && f.Message.StartsWith("line 2"));
        });
    }

    [Fact]
    public void Sessions_are_only_inspected_when_selected()
    {
        WithKnowledgeBase(temp =>
        {
            Write(temp, "_repolore/sessions/a/root.md", "[gone](missing.md)");
            File.WriteAllBytes(Path.Combine(temp, "_repolore", "sessions", "a", "notes.md"), new byte[] { 0xFF });
            Write(temp, "_repolore/sessions/b/root.md", "ok");

            Check(temp).Findings.Should().BeEmpty();

            var selected = Check(temp, session: "a");
            IdsOf(selected).Should().BeEquivalentTo(FindingIds.BrokenLink, FindingIds.InvalidUtf8);
            selected.Findings.Should().OnlyContain(f => f.Path.StartsWith("_repolore/sessions/a/"));
        });
    }

    [Fact]
    public void A_missing_session_and_a_missing_session_root_are_invalid_sessions()
    {
        WithKnowledgeBase(temp =>
        {
            IdsOf(Check(temp, session: "absent")).Should().Equal(FindingIds.InvalidSession);

            Directory.CreateDirectory(Path.Combine(temp, "_repolore", "sessions", "norot"));
            Check(temp, session: "norot").Findings.Should().ContainSingle(f => f.Id == FindingIds.InvalidSession && f.Path.EndsWith("/root.md"));
        });
    }

    [Fact]
    public void A_durable_link_into_a_session_is_a_portability_finding_and_a_session_link_to_another_session_is_not_followed()
    {
        WithKnowledgeBase(temp =>
        {
            Write(temp, "_repolore/root.md", "[s](sessions/a/root.md)");
            Write(temp, "_repolore/sessions/a/root.md", "[other](../b/root.md) [self](notes.md)");
            Write(temp, "_repolore/sessions/a/notes.md", "ok");

            var report = Check(temp, session: "a");
            IdsOf(report).Should().Equal(FindingIds.SessionLinkInDurableNote);
        });
    }

    [Fact]
    public void A_history_path_that_is_not_a_directory_is_unavailable()
    {
        WithKnowledgeBase(temp =>
        {
            Write(temp, "_repolore/.history", "a file");
            IdsOf(Check(temp)).Should().Equal(FindingIds.HistoryUnavailable);
        });
    }

    [Fact]
    public void Corrupt_manifests_and_missing_objects_are_corrupt_history_while_a_valid_history_is_healthy()
    {
        WithKnowledgeBase(temp =>
        {
            var history = Path.Combine(temp, "_repolore", ".history");
            new CheckpointEngine(temp, history).Capture(HistoryConfigLoader.Load(temp));
            Check(temp).Findings.Should().BeEmpty();

            File.Delete(Directory.EnumerateFiles(Path.Combine(history, "objects")).First());
            IdsOf(Check(temp)).Should().Equal(FindingIds.HistoryCorrupt);

            File.WriteAllText(Path.Combine(history, "checkpoints", ManifestId.Format(2) + ".json"), "{broken");
            Check(temp).Findings.Should().ContainSingle(f => f.Id == FindingIds.HistoryCorrupt && f.Path.EndsWith("/checkpoints"));
        });
    }

    [Fact]
    public void A_pending_record_is_reported_with_its_recovery_id_and_a_corrupt_one_is_corrupt_history()
    {
        WithKnowledgeBase(temp =>
        {
            var history = Path.Combine(temp, "_repolore", ".history");
            new PendingStore(history).Write(new PendingTransaction(HistoryVersion.Current, 1, 7, 1, true, 1000, new List<string>(), new List<PendingPlanEntry>()));
            Check(temp).Findings.Should().ContainSingle(f => f.Id == FindingIds.HistoryPending && f.Message.Contains("restore 7"));

            File.WriteAllText(Path.Combine(history, "pending.json"), "{broken");
            IdsOf(Check(temp)).Should().Equal(FindingIds.HistoryCorrupt);
        });
    }

    [Fact]
    public void Explain_reports_discovery_and_history_decisions_and_rejects_escaping_paths()
    {
        WithKnowledgeBase(temp =>
        {
            var explanation = Check(temp, explain: "src/build/a.txt").Explanation!;
            explanation.SourceDiscovery.Excluded.Should().BeTrue();
            explanation.HistoryCovered.Should().BeFalse();

            new Action(() => Check(temp, explain: "../outside.txt")).Should().Throw<PathResolutionException>();
        });
    }

    private static HealthReport Check(string root, string? session = null, string? explain = null) =>
        new HealthChecker(root).Check(session, explain);

    private static string[] IdsOf(HealthReport report) => report.Findings.Select(f => f.Id).ToArray();

    private static void Write(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static void WithKnowledgeBase(Action<string> action)
    {
        TempDir.WithTemp(temp =>
        {
            Write(temp, "_repolore/repolore.json", "{\"formatVersion\":1}");
            Write(temp, "_repolore/method.md", "method");
            Write(temp, "_repolore/root.md", "root");
            Write(temp, "_repolore/sparse-tree/src/src.md", "source knowledge");
            Directory.CreateDirectory(Path.Combine(temp, "src"));
            action(temp);
        });
    }

    private static string Snapshot(string root) => string.Join('\n',
        Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => Directory.Exists(path) ? "D " + path : "F " + path + " " + File.GetLastWriteTimeUtc(path).Ticks + " " + File.ReadAllBytes(path).Length));
}
