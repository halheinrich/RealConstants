using static HalHeinrich.Numerics.Tests.Enclosures;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="ZetaThreeReference"/> itself, which is an oracle and therefore has to earn its
/// authority before anything is tested against it.
/// </summary>
/// <remarks>
/// The digit string is checked from above and below by two things that share nothing with each
/// other: the defining series, which fixes its leading places from the definition of zeta(3)
/// alone, and a deep provider enclosure, which fixes all sixty. Neither alone would do. The
/// defining series cannot reach sixty places at any reasonable cost, and a provider checking the
/// string it is then tested against would be the pi pair's mild circularity repeated.
/// </remarks>
public class ZetaThreeReferenceTests
{
    [Fact]
    public void TheDefiningSeriesEnclosesZetaThree_FromTheDefinitionAlone()
    {
        // No provider, no acceleration, no typed digits: the partial sum of 1/k^3 with the tail
        // bracketed by integrals of x^-3. A thousand terms pins about nine places.
        Approximation fromDefinition = ZetaThreeReference.FromDefinition(1000);

        Assert.True(fromDefinition.MaxError < TenToTheMinus(9));
        Assert.True(fromDefinition.MaxError.Sign > 0);

        Assert.True(
            ZetaThreeReference.AgreesWith(fromDefinition),
            Inv($"the defining series and the reference digits disagree: {fromDefinition.Value}"));
        Assert.True(
            fromDefinition.Contains(ZetaThreeReference.Lower),
            "the reference's truncated value is outside the defining series' enclosure");
    }

    [Fact]
    public void TheDefiningSeriesEnclosureNarrowsWithMoreTerms()
    {
        // The tail bracket is 1/(2*(N+1)^2) below and 1/(2*N^2) above, so the half-width falls
        // like 1/(2*N^3). Checked because a tail bound that did not tighten would make the
        // previous test pass for the wrong reason.
        Approximation coarse = ZetaThreeReference.FromDefinition(50);
        Approximation fine = ZetaThreeReference.FromDefinition(500);

        Assert.True(fine.MaxError < coarse.MaxError);
        Assert.True(Enclosures.Meet(coarse, fine));
    }

    [Fact]
    public void TheDefiningSeriesRejectsANonPositiveTermCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ZetaThreeReference.FromDefinition(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ZetaThreeReference.FromDefinition(-1));
    }

    [Fact]
    public void TheReferenceDigitsAreConfirmedByBothProviders_EachFinerThanTheDigitsAre()
    {
        // Two computations that share no code, each with a proven bound past the sixtieth place,
        // each required to sit wholly inside the interval those digits license. One mistyped
        // digit moves that interval by at least 1e-60 and fails both halves of this.
        Approximation apery = new AperyZetaThree().Refinements().Skip(100).First();
        Approximation borwein = new BorweinZetaThree().Refinements().Skip(90).First();

        Assert.True(apery.MaxError < TenToTheMinus(63));
        Assert.True(borwein.MaxError < TenToTheMinus(63));

        Assert.True(ZetaThreeReference.LiesWithin(apery), "Apery's deep enclosure left the reference");
        Assert.True(ZetaThreeReference.LiesWithin(borwein), "Borwein's deep enclosure left the reference");
    }

    [Fact]
    public void TheReferenceWouldRejectAWrongDigit()
    {
        // The reference's predicates have to be capable of failing, or every test built on them
        // is vacuous. A displacement of 1e-55 is far larger than the sixtieth-place window and
        // far smaller than any provider bound tested against it, so it must be refused.
        Approximation shifted = Approximation.Create(
            ZetaThreeReference.Lower + TenToTheMinus(55),
            TenToTheMinus(58));

        Assert.False(ZetaThreeReference.AgreesWith(shifted));
        Assert.False(ZetaThreeReference.LiesWithin(shifted));
        Assert.False(ZetaThreeReference.Pins(shifted));
    }
}
