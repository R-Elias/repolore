using FluentAssertions;
using RepoLore.Core.Restore;
using RepoLore.Core.Snapshot;
using RepoLore.Infrastructure.History;

namespace RepoLore.Cli.Tests;

public static class Arrangements
{
    public static void Apply(string kind, string temp)
    {
        switch (kind)
        {
            case "empty":
                break;
            case "minimal":
                TestSupport.CopyTree(FixturePath("minimal-v1"), temp);
                AddSourceDirectory(temp);
                break;
            case "checkpointed":
                Apply("minimal", temp);
                TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);
                File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "edited after the first checkpoint\n");
                break;
            case "alpha-sparse":
                TestSupport.CopyTree(FixturePath("alpha-sparse-only"), temp);
                break;
            case "alpha-conflict":
                TestSupport.CopyTree(FixturePath("alpha-conflict"), temp);
                break;
            case "disabled-history":
                Apply("minimal", temp);
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1,\"history\":{\"enabled\":false}}");
                break;
            case "unsupported-format":
                Apply("minimal", temp);
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":2}");
                break;
            case "corrupt-history":
                Apply("minimal", temp);
                var checkpoints = Path.Combine(temp, "_repolore", ".history", "checkpoints");
                Directory.CreateDirectory(checkpoints);
                File.WriteAllText(Path.Combine(checkpoints, ManifestId.Format(1) + ".json"), "{broken");
                break;
            case "pending":
                Apply("minimal", temp);
                var history = Path.Combine(temp, "_repolore", ".history");
                Directory.CreateDirectory(history);
                File.WriteAllBytes(Path.Combine(history, "write.lock"), Array.Empty<byte>());
                new PendingStore(history).Write(
                    new PendingTransaction(HistoryVersion.Current, 1, 7, 1, true, 1000, new List<string>(), new List<PendingPlanEntry>()));
                break;
            default:
                throw new ArgumentException("unknown arrangement: " + kind);
        }
    }

    public static void AddSourceDirectory(string temp)
    {
        Directory.CreateDirectory(Path.Combine(temp, "src"));
        File.WriteAllText(Path.Combine(temp, "src", "main.txt"), "source sentinel");
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
