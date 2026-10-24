using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RepoLore.Cli.Tests.Packaging.Journey;

namespace RepoLore.Cli.Tests.Scenarios;

public sealed record ScenarioOutcome(IReadOnlyList<string> Failures, string Log)
{
    public bool Passed => Failures.Count == 0;
}

// Runs one scenario in <work>/repo, a fresh clone of the reference repository. The only actions are a checkout of a
// stage and a tool run; the runner never writes into the clone itself. The first failing step ends the scenario.
// Golden files are read from goldenDirectory; when goldenWriteDirectory is set, a missing or different golden file is
// written there for review and the step still fails, so a run that writes golden files never passes.
public sealed class ScenarioRunner(
    Func<string, string[], (int Code, string Out, string Error)> tool,
    string? goldenDirectory = null,
    string? goldenWriteDirectory = null)
{
    private static readonly string[] ReadOnlyCommands = ["context", "path", "tree", "history", "health-check", "version"];

    public ScenarioOutcome Run(Scenario scenario, string referenceRepository, string workDirectory)
    {
        var log = new StringBuilder();
        var failures = new List<string>();
        var repository = Path.Combine(workDirectory, "repo");
        var git = GitRepository.CloneWithoutCheckout(referenceRepository, repository);

        for (var index = 0; index < scenario.Steps.Count && failures.Count == 0; index++)
        {
            var step = scenario.Steps[index];
            var label = $"step {index + 1} ({step.Describe()})";
            log.Append("$ ").AppendLine(step.Describe());
            if (step.Checkout is not null)
            {
                if (git.HasCommit(step.Checkout))
                    git.CheckoutStage(step.Checkout);
                else
                    failures.Add($"{label}: unknown stage '{step.Checkout}' in the reference repository; check the tag or move the pin forward");
                continue;
            }

            var arguments = step.Run!.ToArray();
            var before = IsReadOnly(arguments) ? TreeSnapshot.Take(repository) : null;
            var (code, output, error) = tool(repository, arguments);
            output = TestSupport.Normalize(output);
            error = TestSupport.Normalize(error);
            log.AppendLine($"[exit {code}]").AppendLine("--- stdout").Append(output).AppendLine("--- stderr").Append(error);

            failures.AddRange(Check(step.Expect!, code, output, error, repository, git).Select(failure => $"{label}: {failure}"));
            var after = before is null ? null : TreeSnapshot.Take(repository);
            if (before is not null && after != before)
                failures.Add($"{label}: a read-only command changed the working tree or the history: {TreeSnapshot.Differences(before, after!)}");
        }

        File.WriteAllText(Path.Combine(workDirectory, "log.txt"), log.ToString());
        return new ScenarioOutcome(failures, log.ToString());
    }

    public static bool IsReadOnly(string[] arguments) =>
        ReadOnlyCommands.Contains(arguments[0])
        || arguments.Contains("--dry-run")
        || arguments.Contains("--help")
        || (arguments[0] == "migrate" && arguments.Contains("--check"));

    private List<string> Check(Expectation expect, int code, string output, string error, string repository, GitRepository git)
    {
        var failures = new List<string>();
        if (code != expect.Exit)
            failures.Add($"exit: expected {expect.Exit}, got {code}");
        failures.AddRange(expect.Stdout.Where(text => !output.Contains(text, StringComparison.Ordinal)).Select(text => $"stdout: missing '{text}'"));
        failures.AddRange(expect.StdoutNot.Where(text => output.Contains(text, StringComparison.Ordinal)).Select(text => $"stdout-not: found '{text}'"));
        failures.AddRange(expect.Stderr.Where(text => !error.Contains(text, StringComparison.Ordinal)).Select(text => $"stderr: missing '{text}'"));
        failures.AddRange(expect.StderrNot.Where(text => error.Contains(text, StringComparison.Ordinal)).Select(text => $"stderr-not: found '{text}'"));
        if (expect.Json.Count > 0)
            failures.AddRange(CheckJson(expect.Json, output));

        foreach (var file in expect.FileEquals)
        {
            var full = Path.Combine(repository, file.Path);
            if (!File.Exists(full))
                failures.Add($"file-equals: '{file.Path}' does not exist");
            else if (!File.ReadAllBytes(full).AsSpan().SequenceEqual(git.FileAt(file.At, file.Path)))
                failures.Add($"file-equals: '{file.Path}' differs from its content at {file.At}");
        }

        foreach (var path in expect.FileAbsent)
        {
            var full = Path.Combine(repository, path);
            if (File.Exists(full) || Directory.Exists(full))
                failures.Add($"file-absent: '{path}' exists");
        }

        if (expect.Golden is not null && CheckGolden(expect.Golden, output, repository) is { } goldenFailure)
            failures.Add(goldenFailure);

        return failures;
    }

    private string? CheckGolden(string name, string output, string repository)
    {
        var actual = GoldenText.Mask(output, repository);
        var file = Path.Combine(goldenDirectory ?? ScenarioCatalog.GoldenDirectory, name + ".txt");
        var expected = File.Exists(file) ? TestSupport.Normalize(File.ReadAllText(file)) : null;
        if (expected == actual)
            return null;
        if (goldenWriteDirectory is not null)
        {
            Directory.CreateDirectory(goldenWriteDirectory);
            File.WriteAllText(Path.Combine(goldenWriteDirectory, name + ".txt"), actual);
            return $"golden: wrote {name}.txt for review; a run that writes golden files never passes";
        }
        if (expected is null)
            return $"golden: {name}.txt does not exist; run with {GoldenText.WriteVariable}=1 to write it, then review it";
        return $"golden: stdout differs from {name}.txt: {GoldenText.FirstDifference(expected, actual)}";
    }

    private static IEnumerable<string> CheckJson(IReadOnlyDictionary<string, JsonElement> expected, string output)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(output);
        }
        catch (JsonException)
        {
            return ["json: stdout is not one JSON document"];
        }

        using (document)
        {
            var failures = new List<string>();
            foreach (var (path, value) in expected)
            {
                var actual = Select(document.RootElement, path);
                if (actual is null)
                    failures.Add($"json: '{path}' is absent");
                else if (!JsonElement.DeepEquals(actual.Value, value))
                    failures.Add($"json: '{path}' expected {value.GetRawText()}, got {actual.Value.GetRawText()}");
            }
            return failures;
        }
    }

    // Dotted path: object member names and array indexes, e.g. "error.id" or "checkpoints.0.id".
    private static JsonElement? Select(JsonElement root, string path)
    {
        var current = root;
        foreach (var part in path.Split('.'))
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var member))
                current = member;
            else if (current.ValueKind == JsonValueKind.Array && int.TryParse(part, out var index) && index >= 0 && index < current.GetArrayLength())
                current = current[index];
            else
                return null;
        }
        return current;
    }
}

// Golden output is stdout with line endings normalized, timestamps replaced by <timestamp> and the clone's absolute
// path replaced by <repo>, so it is identical on every machine and operating system.
public static class GoldenText
{
    public const string WriteVariable = "REPOLORE_SCENARIO_WRITE_GOLDEN";

    private static readonly Regex Timestamp = new(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})", RegexOptions.CultureInvariant);

    public static string Mask(string output, string repository)
    {
        var masked = Timestamp.Replace(TestSupport.Normalize(output), "<timestamp>");
        foreach (var path in new[] { repository, repository.Replace('\\', '/') }.Distinct())
            masked = masked.Replace(path, "<repo>", StringComparison.Ordinal);
        return masked;
    }

    public static string FirstDifference(string expected, string actual)
    {
        var want = expected.Split('\n');
        var got = actual.Split('\n');
        for (var line = 0; line < Math.Max(want.Length, got.Length); line++)
        {
            var wanted = line < want.Length ? want[line] : "<end of output>";
            var found = line < got.Length ? got[line] : "<end of output>";
            if (wanted != found)
                return $"line {line + 1}: expected '{wanted}', got '{found}'";
        }
        return "the outputs differ only in their final line ending";
    }
}

// Every entry under a directory, including .git and .history: kind and relative path, plus a file's modification time
// and content hash or a link's target. Symbolic links are recorded, never followed, so dangling or escaping links are
// safe. Directory times are left out (an added or removed entry already shows), and each file's time is re-read from
// the file itself: on Windows, enumeration returns the directory entry's copy of the timestamps, which NTFS updates
// lazily, so it can change between two snapshots without any write.
public static class TreeSnapshot
{
    public static string Take(string root)
    {
        var lines = new List<string>();
        Walk(new DirectoryInfo(root), root, lines);
        lines.Sort(StringComparer.Ordinal);
        return string.Join('\n', lines);
    }

    // The first few lines present in only one snapshot, marked '-' (before) or '+' (after).
    public static string Differences(string before, string after)
    {
        var old = before.Split('\n');
        var current = after.Split('\n');
        var changes = old.Except(current, StringComparer.Ordinal).Select(line => "- " + line)
            .Concat(current.Except(old, StringComparer.Ordinal).Select(line => "+ " + line))
            .ToArray();
        return string.Join("; ", changes.Take(6)) + (changes.Length > 6 ? $"; … {changes.Length - 6} more" : string.Empty);
    }

    private static void Walk(DirectoryInfo directory, string root, List<string> lines)
    {
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            entry.Refresh();
            var relative = Path.GetRelativePath(root, entry.FullName).Replace('\\', '/');
            if (entry.LinkTarget is not null)
                lines.Add($"L {relative} -> {entry.LinkTarget}");
            else if (entry is DirectoryInfo child)
            {
                lines.Add($"D {relative}");
                Walk(child, root, lines);
            }
            else
                lines.Add($"F {relative} {entry.LastWriteTimeUtc.Ticks} {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(entry.FullName)))}");
        }
    }
}
