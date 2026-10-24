using Xunit;

namespace RepoLore.Cli.Tests.Scenarios;

[Collection(ScenarioCollection.Name)]
[Trait("Category", "Scenario")]
public sealed class ScenarioTests(ScenarioFixture fixture)
{
    public static TheoryData<string> Cases() => new(ScenarioCatalog.Selected().Select(scenario => scenario.Name));

    [ScenarioTheory]
    [MemberData(nameof(Cases))]
    public void Scenario_passes_on_the_installed_package(string name)
    {
        var scenario = ScenarioCatalog.Load(name);
        var work = fixture.NewWorkDirectory(name);
        var outcome = new ScenarioRunner(fixture.RunTool).Run(scenario, fixture.ReferenceRepository, work);
        if (outcome.Passed)
            return;

        fixture.KeepForInspection(name, work);
        Assert.Fail(string.Join('\n', outcome.Failures) + "\n\ncommand log:\n" + outcome.Log);
    }
}
