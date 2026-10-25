namespace RepoLore.Cli.Arguments;

public static class CommandRules
{
    private static readonly string[] Common = { "--repo-root", "--json", "--quiet" };

    private static readonly Dictionary<string, string[]> OptionsByCommand = new(StringComparer.Ordinal)
    {
        ["version"] = new[] { "--json" },
        ["path"] = new[] { "--include-method" },
        ["context"] = new[] { "--session", "--node", "--budget-tokens", "--strict", "--include-method" },
        ["tree"] = new[] { "--start" },
        ["checkpoint"] = Array.Empty<string>(),
        ["history"] = Array.Empty<string>(),
        ["restore"] = new[] { "--path", "--dry-run" },
        ["init"] = new[] { "--update-method" },
        ["migrate"] = new[] { "--check", "--dry-run" },
        ["health-check"] = new[] { "--explain", "--session" }
    };

    private static readonly string[] CommandsWithPositional = { "path", "context", "restore" };

    public static void Validate(CommandLine commandLine)
    {
        var command = commandLine.Command;
        if (command is null || !OptionsByCommand.TryGetValue(command, out var specific))
            return;

        foreach (var option in commandLine.SeenOptions)
        {
            var allowed = Array.IndexOf(specific, option) >= 0
                || (command != "version" && Array.IndexOf(Common, option) >= 0);
            if (!allowed)
                throw new UsageException($"{command} does not accept {option}");
        }

        if (command == "migrate" && commandLine.SeenOptions.Contains("--check") && commandLine.SeenOptions.Contains("--dry-run"))
            throw new UsageException("migrate takes --check or --dry-run, not both");

        if (commandLine.Target is not null && Array.IndexOf(CommandsWithPositional, command) < 0)
            throw new UsageException($"{command} takes no positional argument: {commandLine.Target}");
    }
}
