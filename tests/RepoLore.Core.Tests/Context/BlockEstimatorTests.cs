using RepoLore.Core.Context;
using RepoLore.Core.Tests;

namespace RepoLore.Core.Tests.Context;

public static class BlockEstimatorTests
{
    public static void Run()
    {
        TestRunner.Check("block render and charge use the canonical format", () =>
        {
            var block = new ContextBlock("p", "durable", "t", 0);
            TestRunner.Equal("---\n## p [durable]\n\nt\n", block.Render());

            var length = "---\n## p [durable]\n\nt\n".Length;
            TestRunner.Equal((length + 3) / 4, BlockEstimator.ChargeFor("p", "durable", "t"));
        });

        TestRunner.Check("estimator skips a non-fitting note and continues", () =>
        {
            var small1 = new ContextNote("a", "durable", "A");
            var big = new ContextNote("b", "durable", new string('x', 400));
            var small2 = new ContextNote("c", "durable", "C");

            var budget = BlockEstimator.ChargeFor("a", "durable", "A")
                       + BlockEstimator.ChargeFor("c", "durable", "C");
            TestRunner.True(BlockEstimator.ChargeFor("b", "durable", new string('x', 400)) > budget);

            var estimate = BlockEstimator.Estimate(new[] { small1, big, small2 }, budget);
            TestRunner.Equal(2, estimate.Included.Count);
            TestRunner.Equal("a", estimate.Included[0].Path);
            TestRunner.Equal("c", estimate.Included[1].Path);
            TestRunner.Equal(1, estimate.Omissions.Count);
            TestRunner.Equal("b", estimate.Omissions[0].Path);
            TestRunner.Equal("budget", estimate.Omissions[0].Reason);
            TestRunner.Equal(budget, estimate.TotalCharge);
        });

        TestRunner.Check("a budget that fits nothing omits everything", () =>
        {
            var note = new ContextNote("a", "durable", "A");
            var estimate = BlockEstimator.Estimate(new[] { note }, 0);
            TestRunner.Equal(0, estimate.Included.Count);
            TestRunner.Equal(1, estimate.Omissions.Count);
        });
    }
}
