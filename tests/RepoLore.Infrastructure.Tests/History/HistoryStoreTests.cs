using System.Text;
using FluentAssertions;
using RepoLore.Core.Snapshot;
using RepoLore.Infrastructure.History;
using Xunit;

namespace RepoLore.Infrastructure.Tests.History;

public class ObjectStoreTests
{
    [Fact]
    public void Store_validate_and_reuse_share_one_object()
    {
        TempDir.WithTemp(temp =>
        {
            var store = new ObjectStore(Path.Combine(temp, ".history"));
            store.EnsureDirectories();
            var bytes = Encoding.UTF8.GetBytes("OBJECT_SENTINEL");
            var hash = ObjectStore.Hash(bytes);

            store.Store(bytes).Should().Be(hash);
            store.Exists(hash).Should().BeTrue();
            store.Validate(hash).Should().BeTrue();
            Encoding.UTF8.GetString(store.Read(hash)).Should().Be("OBJECT_SENTINEL");

            store.Store(bytes);
            Directory.EnumerateFiles(store.ObjectsDirectory).Count().Should().Be(1);
        });
    }

    [Fact]
    public void A_corrupt_object_is_never_silently_replaced()
    {
        TempDir.WithTemp(temp =>
        {
            var store = new ObjectStore(Path.Combine(temp, ".history"));
            store.EnsureDirectories();
            var bytes = Encoding.UTF8.GetBytes("ORIGINAL");
            var hash = ObjectStore.Hash(bytes);
            store.Store(bytes);

            File.WriteAllBytes(Path.Combine(store.ObjectsDirectory, hash), Encoding.UTF8.GetBytes("TAMPERED"));
            store.Validate(hash).Should().BeFalse();
            new Action(() => store.Store(bytes)).Should().Throw<HistoryStoreException>("corrupt object must fail, not overwrite");
        });
    }
}

public class CheckpointStoreTests
{
    [Fact]
    public void Publish_and_read_the_highest_completed_id()
    {
        TempDir.WithTemp(temp =>
        {
            var store = new CheckpointStore(Path.Combine(temp, ".history"));
            store.EnsureDirectory();
            store.Publish(Manifest(1));
            store.Publish(Manifest(3));
            store.Publish(Manifest(2));

            var all = store.ListCompleted();
            all.Should().HaveCount(3);
            all[0].Id.Should().Be(1L);
            all[1].Id.Should().Be(2L);
            all[2].Id.Should().Be(3L);
            store.ReadHighestCompleted()!.Id.Should().Be(3L);
        });
    }

    [Fact]
    public void Incomplete_and_non_manifest_files_are_ignored()
    {
        TempDir.WithTemp(temp =>
        {
            var store = new CheckpointStore(Path.Combine(temp, ".history"));
            store.EnsureDirectory();
            store.Publish(Manifest(1));

            File.WriteAllText(Path.Combine(store.CheckpointsDirectory, "0000000000000002.tmp-x"), "partial");
            File.WriteAllText(Path.Combine(store.CheckpointsDirectory, "1.json"), "{}");
            File.WriteAllText(Path.Combine(store.CheckpointsDirectory, "notes.txt"), "ignore me");

            var all = store.ListCompleted();
            all.Should().HaveCount(1);
            all[0].Id.Should().Be(1L);
        });
    }

    [Fact]
    public void A_malformed_completed_manifest_is_an_error_not_silently_skipped()
    {
        TempDir.WithTemp(temp =>
        {
            var store = new CheckpointStore(Path.Combine(temp, ".history"));
            store.EnsureDirectory();
            File.WriteAllText(Path.Combine(store.CheckpointsDirectory, "0000000000000001.json"), "{not json");
            new Action(() => store.ListCompleted()).Should().Throw<HistoryStoreException>();
        });
    }

    [Fact]
    public void An_empty_history_directory_yields_no_manifests()
    {
        TempDir.WithTemp(temp =>
        {
            var store = new CheckpointStore(Path.Combine(temp, ".history"));
            store.ListCompleted().Should().HaveCount(0);
            store.ReadHighestCompleted().Should().BeNull();
        });
    }

    private static CheckpointManifest Manifest(long id) =>
        new(id, "t", HistoryVersion.Current, 1,
            new CaptureScope(true, 100, new List<string>(), CaptureScope.RepoLoreJsonPresent),
            new List<FileEntry>());
}
