using RepoLore.Infrastructure.History;
using RepoLore.Infrastructure.Tests;
using RepoLore.Infrastructure.Tests.History;

if (args.Length == 3 && args[0] == "--hold-lock")
{
    using (WriterLock.Acquire(args[1]))
    {
        File.WriteAllText(args[2], "held");
        Thread.Sleep(Timeout.Infinite);
    }
    return 0;
}

return TestRunner.RunAll(() =>
{
    PathResolverTests.Run();
    ObjectStoreTests.Run();
    CheckpointStoreTests.Run();
    WriterLockTests.Run();
    CheckpointEngineTests.Run();
    HistoryCleanupTests.Run();
    RetentionEngineTests.Run();
    WriterLockCrashTests.Run();
});
