using RepoLore.Core.Tests;

return TestRunner.RunAll(() =>
{
    KnowledgeMappingTests.Run();
    MatcherTests.Run();
    ConfigParserTests.Run();
});
