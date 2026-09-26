using RepoLore.Core.Tests;
using RepoLore.Core.Tests.Configuration;
using RepoLore.Core.Tests.Mapping;
using RepoLore.Core.Tests.Matching;
using RepoLore.Core.Tests.Policies;

return TestRunner.RunAll(() =>
{
    KnowledgeMappingTests.Run();
    RuleSetTests.Run();
    SourceDiscoveryPolicyTests.Run();
    HistoryCoveragePolicyTests.Run();
    ConfigParserTests.Run();
});
