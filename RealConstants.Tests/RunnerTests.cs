using HalHeinrich.Numerics.Experiments;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="Runner.Realised"/>: the one place both experiments decide whether an oracle can
/// still resolve an enclosure's error.
/// </summary>
/// <remarks>
/// The walk and <c>compare</c> each made this test for themselves until 2026-09-17, one as
/// <c>&gt;</c> and the other as <c>&lt;=</c>. They agreed, but only by being written carefully
/// twice. What is pinned here is the boundary they now share - it is also the walk's fifth stop
/// rule - and the figure on the resolvable side.
/// </remarks>
public class RunnerTests
{
    private static readonly Approximation Oracle =
        Approximation.Create(new BigRational(3, 1), new BigRational(1, 1000));

    [Fact]
    public void ACoarserEnclosureReportsTheLargestItsErrorCouldBe()
    {
        // |31/10 - 3| widened by the oracle's own half-width: 1/10 + 1/1000.
        Approximation reached = Approximation.Create(new BigRational(31, 10), new BigRational(1, 5));

        Assert.Equal(new BigRational(101, 1000), Runner.Realised(reached, Oracle));
    }

    [Fact]
    public void TheWideningCountsEvenWhenTheValuesCoincide()
    {
        // The oracle is itself an enclosure, so an exact match in value is still only known to
        // within its half-width. Reporting zero here would claim a precision nobody has.
        Approximation reached = Approximation.Create(new BigRational(3, 1), new BigRational(1, 5));

        Assert.Equal(new BigRational(1, 1000), Runner.Realised(reached, Oracle));
    }

    [Fact]
    public void AnEnclosureAsFineAsTheOracleIsPastIt()
    {
        // The boundary itself. At equal half-widths the widening is as large as anything the
        // enclosure claims, so no figure it produced could be told from the oracle's own slack.
        Approximation reached = Approximation.Create(new BigRational(3, 1), new BigRational(1, 1000));

        Assert.Null(Runner.Realised(reached, Oracle));
    }

    [Fact]
    public void AnEnclosureFinerThanTheOracleIsPastIt()
    {
        Approximation reached = Approximation.Create(new BigRational(3, 1), new BigRational(1, 2000));

        Assert.Null(Runner.Realised(reached, Oracle));
    }
}
