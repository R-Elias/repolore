using RepoLore.Infrastructure.History;

if (args.Length == 3 && args[0] == "--hold-lock")
{
    using (WriterLock.Acquire(args[1]))
    {
        File.WriteAllText(args[2], "held");
        Thread.Sleep(Timeout.Infinite);
    }
    return 0;
}

return 0;
