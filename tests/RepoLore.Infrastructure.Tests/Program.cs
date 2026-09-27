using RepoLore.Infrastructure.Tests;
using RepoLore.Infrastructure.Tests.History;

return TestRunner.RunAll(() =>
{
    PathResolverTests.Run();
    ObjectStoreTests.Run();
    CheckpointStoreTests.Run();
    WriterLockTests.Run();
    CheckpointEngineTests.Run();
});
