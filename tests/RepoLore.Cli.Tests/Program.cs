using RepoLore.Cli.Tests;

return TestRunner.RunAll(() =>
{
    ExecutableFoundationTests.Run();
    DurableSessionContextTests.Run();
    HistoryTests.Run();
    RestoreTests.Run();
    InitTests.Run();
});
