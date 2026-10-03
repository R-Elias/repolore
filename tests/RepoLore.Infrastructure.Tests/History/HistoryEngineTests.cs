using FluentAssertions;
using RepoLore.Infrastructure.History;
using Xunit;

namespace RepoLore.Infrastructure.Tests.History;

public class WriterLockTests
{
    [Fact]
    public void The_lock_is_acquired_and_released()
    {
        TempDir.WithTemp(temp =>
        {
            var history = Path.Combine(temp, ".history");
            using (WriterLock.Acquire(history))
                File.Exists(Path.Combine(history, "write.lock")).Should().BeTrue();
            using (WriterLock.Acquire(history))
                true.Should().BeTrue();
        });
    }

    [Fact]
    public void A_second_writer_is_refused()
    {
        TempDir.WithTemp(temp =>
        {
            var history = Path.Combine(temp, ".history");
            using (WriterLock.Acquire(history))
                new Action(() => WriterLock.Acquire(history)).Should().Throw<WriterLockException>();
        });
    }
}

public class CheckpointEngineTests
{
    private const string Root = "_repolore/root.md";
    private const string A = "_repolore/architecture/a.md";

    [Fact]
    public void First_capture_publishes_a_manifest_and_content_objects()
    {
        WithRepo(temp =>
        {
            var engine = NewEngine(temp);
            var result = engine.Capture(HistoryConfigLoader.Load(temp));

            result.WasNoOp.Should().BeFalse();
            result.PublishedId.Should().Be(1L);
            result.Added.Contains(A).Should().BeTrue();
            File.Exists(Path.Combine(temp, "_repolore", ".history", "checkpoints", "0000000000000001.json")).Should().BeTrue();
            HighestId(temp).Should().Be(1L);
        });
    }

    [Fact]
    public void Unchanged_state_is_a_no_op()
    {
        WithRepo(temp =>
        {
            var engine = NewEngine(temp);
            engine.Capture(HistoryConfigLoader.Load(temp));

            var second = engine.Capture(HistoryConfigLoader.Load(temp));
            second.WasNoOp.Should().BeTrue();
            second.PublishedId.Should().BeNull();
            HighestId(temp).Should().Be(1L);
        });
    }

    [Fact]
    public void Add_change_and_delete_produce_distinct_manifests()
    {
        WithRepo(temp =>
        {
            var engine = NewEngine(temp);
            engine.Capture(HistoryConfigLoader.Load(temp));

            File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "a.md"), "A_EDITED");
            File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "b.md"), "B_NEW");
            var second = engine.Capture(HistoryConfigLoader.Load(temp));
            second.PublishedId.Should().Be(2L);
            second.Changed.Contains(A).Should().BeTrue();
            second.Added.Contains("_repolore/architecture/b.md").Should().BeTrue();

            File.Delete(Path.Combine(temp, "_repolore", "architecture", "a.md"));
            var third = engine.Capture(HistoryConfigLoader.Load(temp));
            third.PublishedId.Should().Be(3L);
            third.Removed.Contains(A).Should().BeTrue();
        });
    }

    [Fact]
    public void A_file_changing_between_the_two_capture_passes_fails_without_publishing()
    {
        WithRepo(temp =>
        {
            var engine = NewEngine(temp);
            var touched = false;

            var ex = new Action(() => engine.Capture(HistoryConfigLoader.Load(temp), beforeVerification: () =>
            {
                if (!touched)
                {
                    touched = true;
                    File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT_CHANGED");
                }
            })).Should().Throw<HistoryStoreException>().Which;

            ex.Message.Should().Contain("changed while capturing");
            HighestId(temp).Should().Be(0L);
        });
    }

    [Fact]
    public void Failure_before_manifest_publication_leaves_the_previous_checkpoint_valid()
    {
        WithRepo(temp =>
        {
            var engine = NewEngine(temp);
            engine.Capture(HistoryConfigLoader.Load(temp));

            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT_CHANGED");
            var ex = new Action(() => engine.Capture(HistoryConfigLoader.Load(temp), beforeManifestPublish: () => throw new HistoryStoreException("injected failure")))
                .Should().Throw<HistoryStoreException>().Which;

            ex.Message.Should().Contain("injected failure");
            HighestId(temp).Should().Be(1L);
            File.ReadAllText(Path.Combine(temp, "_repolore", "root.md")).Should().Be("ROOT_CHANGED");
        });
    }

    [Fact]
    public void Distinct_paths_with_identical_bytes_share_one_object()
    {
        WithRepo(temp =>
        {
            File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "c.md"), "SAME");
            File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "d.md"), "SAME");

            var engine = NewEngine(temp);
            engine.Capture(HistoryConfigLoader.Load(temp));

            var objects = Directory.EnumerateFiles(Path.Combine(temp, "_repolore", ".history", "objects")).Count();
            // root.md, repolore.json, a.md, and the shared c.md/d.md -> four distinct contents
            objects.Should().Be(4);
        });
    }

    private static CheckpointEngine NewEngine(string temp) =>
        new(temp, Path.Combine(temp, "_repolore", ".history"), new Clock(() => new DateTimeOffset(2026, 9, 27, 11, 0, 0, TimeSpan.Zero)));

    private static long HighestId(string temp)
    {
        var store = new CheckpointStore(Path.Combine(temp, "_repolore", ".history"));
        var highest = store.ReadHighestCompleted();
        return highest is null ? 0 : highest.Id;
    }

    private static void WriteRepo(string temp)
    {
        Directory.CreateDirectory(Path.Combine(temp, "_repolore", "architecture"));
        File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
        File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT");
        File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "a.md"), "A_ORIGINAL");
    }

    private static void WithRepo(Action<string> action)
    {
        TempDir.WithTemp(temp =>
        {
            WriteRepo(temp);
            action(temp);
        });
    }
}
