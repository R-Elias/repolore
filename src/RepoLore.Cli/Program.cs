using RepoLore.Cli;
using RepoLore.Core;

if (args is ["version"])
{
    Console.WriteLine($"RepoLore {CliVersion.Value}");
    Console.WriteLine($"Supported knowledge formats: {KnowledgeFormat.Current}");
    return 0;
}

if (args is ["--help"])
{
    Console.WriteLine("Usage: repolore version | --help");
    Console.WriteLine("Only the executable foundation is implemented; knowledge commands are not available yet.");
    return 0;
}

Console.Error.WriteLine("Usage: repolore version | --help");
return 2;
