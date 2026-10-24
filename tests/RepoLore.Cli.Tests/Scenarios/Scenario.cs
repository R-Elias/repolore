using System.Text.Json;

namespace RepoLore.Cli.Tests.Scenarios;

public sealed record Scenario(string Name, IReadOnlyList<string> Tags, IReadOnlyList<string> Os, IReadOnlyList<ScenarioStep> Steps);

public sealed record ScenarioStep(string? Checkout, IReadOnlyList<string>? Run, Expectation? Expect)
{
    public string Describe() => Checkout is not null ? "checkout " + Checkout : "repolore " + string.Join(' ', Run!);
}

public sealed record FileAtStage(string Path, string At);

public sealed record Expectation(
    int Exit,
    IReadOnlyList<string> Stdout,
    IReadOnlyList<string> StdoutNot,
    IReadOnlyList<string> Stderr,
    IReadOnlyList<string> StderrNot,
    IReadOnlyDictionary<string, JsonElement> Json,
    IReadOnlyList<FileAtStage> FileEquals,
    IReadOnlyList<string> FileAbsent);

// Reads one scenario file strictly: an unknown key, a missing exit code or a misplaced step is an error, so a typo
// can never turn an expectation into a no-op.
public static class ScenarioFile
{
    public static readonly string[] OperatingSystems = ["windows", "linux", "macos"];

    private static readonly string[] ScenarioKeys = ["name", "description", "tags", "os", "steps"];
    private static readonly string[] ExpectKeys = ["exit", "stdout", "stdout-not", "stderr", "stderr-not", "json", "file-equals", "file-absent"];

    private static readonly JsonDocumentOptions Options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static Scenario Parse(string fileName, string text)
    {
        using var document = JsonDocument.Parse(text, Options);
        var root = document.RootElement;
        RequireObject(root, "scenario");
        RequireKnownKeys(root, ScenarioKeys, "scenario");

        var name = RequiredString(root, "name");
        if (name != fileName)
            throw new FormatException($"scenario name '{name}' must equal its file name '{fileName}'");

        var os = root.TryGetProperty("os", out var osElement) ? Strings(osElement, "os") : OperatingSystems;
        foreach (var value in os)
            if (!OperatingSystems.Contains(value))
                throw new FormatException($"unknown os '{value}'; use {string.Join(", ", OperatingSystems)}");

        var tags = root.TryGetProperty("tags", out var tagsElement) ? Strings(tagsElement, "tags") : [];

        if (!root.TryGetProperty("steps", out var stepsElement) || stepsElement.ValueKind != JsonValueKind.Array || stepsElement.GetArrayLength() == 0)
            throw new FormatException("'steps' must be a non-empty array");
        var steps = stepsElement.EnumerateArray().Select((step, index) => ParseStep(step, index + 1)).ToArray();
        if (steps[0].Checkout is null)
            throw new FormatException("the first step must be a checkout");

        return new Scenario(name, tags, os, steps);
    }

    private static ScenarioStep ParseStep(JsonElement step, int number)
    {
        var label = $"step {number}";
        RequireObject(step, label);
        if (step.TryGetProperty("checkout", out var checkout))
        {
            RequireKnownKeys(step, ["checkout"], label);
            if (checkout.ValueKind != JsonValueKind.String || checkout.GetString()!.Length == 0)
                throw new FormatException($"{label}: 'checkout' must name a tag");
            return new ScenarioStep(checkout.GetString(), null, null);
        }

        RequireKnownKeys(step, ["run", "expect"], label);
        if (!step.TryGetProperty("run", out var run))
            throw new FormatException($"{label}: a step is either a 'checkout' or a 'run'");
        var arguments = run.ValueKind == JsonValueKind.String
            ? run.GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            : Strings(run, label + " run");
        if (arguments.Length == 0)
            throw new FormatException($"{label}: 'run' must name a command");
        if (!step.TryGetProperty("expect", out var expect))
            throw new FormatException($"{label}: a run step needs 'expect' with at least 'exit'");
        return new ScenarioStep(null, arguments, ParseExpectation(expect, label));
    }

    private static Expectation ParseExpectation(JsonElement expect, string label)
    {
        RequireObject(expect, label + " expect");
        RequireKnownKeys(expect, ExpectKeys, label + " expect");
        if (!expect.TryGetProperty("exit", out var exit) || !exit.TryGetInt32(out var code))
            throw new FormatException($"{label}: 'expect.exit' is required and must be an integer");

        var json = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (expect.TryGetProperty("json", out var jsonElement))
        {
            RequireObject(jsonElement, label + " json");
            foreach (var property in jsonElement.EnumerateObject())
                json[property.Name] = property.Value.Clone();
        }

        var fileEquals = new List<FileAtStage>();
        if (expect.TryGetProperty("file-equals", out var equalsElement))
        {
            var items = equalsElement.ValueKind == JsonValueKind.Array ? equalsElement.EnumerateArray().ToArray() : [equalsElement];
            foreach (var item in items)
            {
                RequireObject(item, label + " file-equals");
                RequireKnownKeys(item, ["path", "at"], label + " file-equals");
                fileEquals.Add(new FileAtStage(RequiredString(item, "path"), RequiredString(item, "at")));
            }
        }

        return new Expectation(
            code,
            OptionalStrings(expect, "stdout", label),
            OptionalStrings(expect, "stdout-not", label),
            OptionalStrings(expect, "stderr", label),
            OptionalStrings(expect, "stderr-not", label),
            json,
            fileEquals,
            OptionalStrings(expect, "file-absent", label));
    }

    private static string[] OptionalStrings(JsonElement element, string key, string label) =>
        element.TryGetProperty(key, out var value) ? Strings(value, label + " " + key) : [];

    private static string[] Strings(JsonElement element, string label)
    {
        if (element.ValueKind == JsonValueKind.String)
            return [element.GetString()!];
        if (element.ValueKind != JsonValueKind.Array || element.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
            throw new FormatException($"{label}: expected a string or an array of strings");
        return element.EnumerateArray().Select(item => item.GetString()!).ToArray();
    }

    private static string RequiredString(JsonElement element, string key)
    {
        if (!element.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String || value.GetString()!.Length == 0)
            throw new FormatException($"'{key}' is required and must be a non-empty string");
        return value.GetString()!;
    }

    private static void RequireObject(JsonElement element, string label)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new FormatException($"{label} must be a JSON object");
    }

    private static void RequireKnownKeys(JsonElement element, string[] allowed, string label)
    {
        foreach (var property in element.EnumerateObject())
            if (!allowed.Contains(property.Name))
                throw new FormatException($"{label}: unknown key '{property.Name}'");
    }
}
