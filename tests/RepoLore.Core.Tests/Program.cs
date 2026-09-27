using RepoLore.Core.Tests;
using RepoLore.Core.Tests.Configuration;
using RepoLore.Core.Tests.Context;
using RepoLore.Core.Tests.Mapping;
using RepoLore.Core.Tests.Matching;
using RepoLore.Core.Tests.Policies;
using RepoLore.Core.Tests.Snapshot;

return TestRunner.RunAll(() =>
{
    KnowledgeMappingTests.Run();
    RuleSetTests.Run();
    SourceDiscoveryPolicyTests.Run();
    HistoryCoveragePolicyTests.Run();
    ConfigParserTests.Run();
    SessionIdTests.Run();
    ContextSelectorTests.Run();
    BlockEstimatorTests.Run();
    NoteReadTests.Run();
    ManifestIdTests.Run();
    ManifestCodecTests.Run();
    SnapshotDifferTests.Run();
});
