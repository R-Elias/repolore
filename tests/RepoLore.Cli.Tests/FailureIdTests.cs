using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace RepoLore.Cli.Tests;

// Error IDs that need a concurrent writer or a failing write, which checking out a scenario stage cannot produce.
public sealed class FailureIdTests
{
    private const string Note = "_repolore/sparse-tree/src/src.md";

    [Fact]
    public void Checkpoint_while_another_writer_holds_the_lock_fails_with_history_locked()
    {
        TestSupport.WithFixture("minimal-v1", temp =>
        {
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);
            File.AppendAllText(Path.Combine(temp, Note), "edited\n");

            (int Code, string Out, string Error) result;
            using (new FileStream(Path.Combine(temp, "_repolore", ".history", "write.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                result = TestSupport.Run(temp, TestSupport.Cli, "checkpoint", "--json");

            result.Code.Should().Be(3, result.Error);
            using var error = JsonDocument.Parse(result.Out);
            error.RootElement.GetProperty("error").GetProperty("id").GetString().Should().Be("history-locked");
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Out.Should().Contain("checkpoint 0000000000000002", "the lock is released with its holder");
        });
    }

    [Fact]
    public void A_restore_that_cannot_write_a_file_is_interrupted_and_names_its_recovery_id()
    {
        TestSupport.WithFixture("minimal-v1", temp =>
        {
            var note = Path.Combine(temp, Note);
            var original = File.ReadAllBytes(note);
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);
            File.AppendAllText(note, "edited\n");
            var edited = File.ReadAllBytes(note);
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Out.Should().Contain("checkpoint 0000000000000002");

            var result = WithNoteUnwritable(note, () => TestSupport.Run(temp, TestSupport.Cli, "restore", "1", "--json"));

            result.Code.Should().Be(3, result.Error);
            using (var error = JsonDocument.Parse(result.Out))
            {
                error.RootElement.GetProperty("error").GetProperty("id").GetString().Should().Be("operation-interrupted");
                error.RootElement.GetProperty("error").GetProperty("recoveryId").GetInt64().Should().Be(2);
            }
            File.Exists(Path.Combine(temp, "_repolore", ".history", "pending.json")).Should().BeTrue();

            var recover = TestSupport.Run(temp, TestSupport.Cli, "restore", "2");
            recover.Code.Should().Be(0, recover.Error);
            File.ReadAllBytes(note).Should().Equal(edited);
            File.Exists(Path.Combine(temp, "_repolore", ".history", "pending.json")).Should().BeFalse();
            original.Should().NotEqual(edited);
        });
    }

    [Theory]
    [InlineData("checkpoint")]
    [InlineData("restore 1")]
    [InlineData("init")]
    public void A_file_in_place_of_the_history_directory_fails_writes_with_a_clear_message_and_no_machine_path(string command)
    {
        TestSupport.WithFixture("minimal-v1", temp =>
        {
            var history = Path.Combine(temp, "_repolore", ".history");
            File.WriteAllText(history, "not a directory\n");

            var result = TestSupport.Run(temp, [TestSupport.Cli, .. command.Split(' '), "--json"]);

            result.Code.Should().Be(3, result.Error);
            using var error = JsonDocument.Parse(result.Out);
            error.RootElement.GetProperty("error").GetProperty("id").GetString().Should().Be("io-error");
            error.RootElement.GetProperty("error").GetProperty("message").GetString().Should().Be(
                "_repolore/.history is a file, not a directory, so nothing can be written; move that file aside or delete it, then retry");
            (result.Out + result.Error).Should().NotContain(temp);
            File.ReadAllText(history).Should().Be("not a directory\n");
        });
    }

    // Windows refuses to write, replace or delete a file another handle holds with read-only sharing (the restore's own
    // pre-operation checkpoint can still read it); Unix refuses to change the entries of a directory without write
    // permission.
    private static T WithNoteUnwritable<T>(string note, Func<T> action)
    {
        if (OperatingSystem.IsWindows())
        {
            using var held = new FileStream(note, FileMode.Open, FileAccess.Read, FileShare.Read);
            return action();
        }

        var directory = Path.GetDirectoryName(note)!;
        var mode = File.GetUnixFileMode(directory);
        File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            return action();
        }
        finally
        {
            File.SetUnixFileMode(directory, mode);
        }
    }
}
