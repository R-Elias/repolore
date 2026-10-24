namespace RepoLore.Cli.Tests.Scenarios;

public static class ScenarioCatalog
{
    public const string TagsVariable = "REPOLORE_SCENARIO_TAGS";

    public static string CasesDirectory => Path.Combine(AppContext.BaseDirectory, "Scenarios", "Cases");

    public static string GoldenDirectory => Path.Combine(CasesDirectory, "golden");

    // Where REPOLORE_SCENARIO_WRITE_GOLDEN=1 writes golden files: the source tree, so they show up in the Git diff.
    public static string GoldenSourceDirectory => Path.Combine(TestSupport.Repo, "tests", "RepoLore.Cli.Tests", "Scenarios", "Cases", "golden");

    public static string CurrentOs =>
        OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux";

    public static IReadOnlyList<Scenario> All() =>
        Directory.GetFiles(CasesDirectory, "*.jsonc")
            .Order(StringComparer.Ordinal)
            .Select(file => Load(Path.GetFileNameWithoutExtension(file)))
            .ToArray();

    public static Scenario Load(string name)
    {
        var file = Path.Combine(CasesDirectory, name + ".jsonc");
        try
        {
            return ScenarioFile.Parse(name, File.ReadAllText(file));
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException)
        {
            throw new FormatException($"{name}.jsonc: {ex.Message}", ex);
        }
    }

    // Scenarios for this operating system, narrowed to the comma-separated tags in REPOLORE_SCENARIO_TAGS when set.
    public static IReadOnlyList<Scenario> Selected() =>
        Select(All(), CurrentOs, Environment.GetEnvironmentVariable(TagsVariable) ?? string.Empty);

    public static IReadOnlyList<Scenario> Select(IEnumerable<Scenario> scenarios, string os, string tagFilter)
    {
        var tags = tagFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return scenarios
            .Where(scenario => scenario.Os.Contains(os))
            .Where(scenario => tags.Length == 0 || scenario.Tags.Intersect(tags, StringComparer.Ordinal).Any())
            .ToArray();
    }
}

// The exact fixture version this suite was written against: the repository URL (used by CI to clone it), a stage tag,
// and the commit that tag must resolve to. The pin only moves forward.
public sealed record ReferencePin(string Repository, string Tag, string Commit)
{
    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "Scenarios", "reference-repo.version");

    public static ReferencePin Load() => Parse(File.ReadAllLines(FilePath));

    public static ReferencePin Parse(IEnumerable<string> lines)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines.Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#')))
        {
            var parts = line.Split(' ', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !values.TryAdd(parts[0], parts[1]))
                throw new FormatException($"reference-repo.version: malformed or repeated line '{line}'");
        }
        foreach (var key in new[] { "repository", "tag", "commit" })
            if (!values.ContainsKey(key))
                throw new FormatException($"reference-repo.version: missing '{key}'");
        if (values.Count != 3)
            throw new FormatException("reference-repo.version: only 'repository', 'tag' and 'commit' are allowed");
        if (values["commit"].Length != 40 || !values["commit"].All(Uri.IsHexDigit))
            throw new FormatException("reference-repo.version: 'commit' must be a full 40-character hash");
        return new ReferencePin(values["repository"], values["tag"], values["commit"]);
    }
}
