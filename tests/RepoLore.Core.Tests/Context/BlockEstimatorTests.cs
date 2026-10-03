using FluentAssertions;
using RepoLore.Core.Context;
using Xunit;

namespace RepoLore.Core.Tests.Context;

public class BlockEstimatorTests
{
    [Fact]
    public void Block_render_and_charge_use_the_canonical_format()
    {
        var block = new ContextBlock("p", "durable", "t", 0);
        block.Render().Should().Be("---\n## p [durable]\n\nt\n");

        var length = "---\n## p [durable]\n\nt\n".Length;
        BlockEstimator.ChargeFor("p", "durable", "t").Should().Be((length + 3) / 4);
    }

    [Fact]
    public void Estimator_skips_a_non_fitting_note_and_continues()
    {
        var small1 = new ContextNote("a", "durable", "A");
        var big = new ContextNote("b", "durable", new string('x', 400));
        var small2 = new ContextNote("c", "durable", "C");

        var budget = BlockEstimator.ChargeFor("a", "durable", "A")
                   + BlockEstimator.ChargeFor("c", "durable", "C");
        BlockEstimator.ChargeFor("b", "durable", new string('x', 400)).Should().BeGreaterThan(budget);

        var estimate = BlockEstimator.Estimate(new[] { small1, big, small2 }, budget);
        estimate.Included.Should().HaveCount(2);
        estimate.Included[0].Path.Should().Be("a");
        estimate.Included[1].Path.Should().Be("c");
        estimate.Omissions.Should().HaveCount(1);
        estimate.Omissions[0].Path.Should().Be("b");
        estimate.Omissions[0].Reason.Should().Be("budget");
        estimate.TotalCharge.Should().Be(budget);
    }

    [Fact]
    public void A_budget_that_fits_nothing_omits_everything()
    {
        var note = new ContextNote("a", "durable", "A");
        var estimate = BlockEstimator.Estimate(new[] { note }, 0);
        estimate.Included.Should().BeEmpty();
        estimate.Omissions.Should().HaveCount(1);
    }
}
