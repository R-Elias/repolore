using RepoLore.Core.Configuration;

namespace RepoLore.Cli.Repository;

public static class FormatGate
{
    public static void Require(string repositoryRoot)
    {
        var marker = Path.Combine(repositoryRoot, "_repolore", "repolore.json");
        if (File.Exists(marker))
            ConfigParser.Parse(File.ReadAllText(marker));
    }
}
