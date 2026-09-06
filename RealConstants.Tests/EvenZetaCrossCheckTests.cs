using static HalHeinrich.Numerics.Tests.Enclosures;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The provider cross-check for the even positive controls: <see cref="CentralBinomialZeta"/> and
/// <see cref="EulerMaclaurinZeta"/> compute zeta(2) and zeta(4) by schemes that share no code, so
/// their enclosures must overlap.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes the even controls of <c>SPEC-rational-ratio.md</c> section 4 genuine
/// cross-check pairs rather than lone providers. A single provider for zeta(2) would leave the
/// control resting on one implementation, and the control's whole job is to catch a pipeline
/// that is wrong.
/// </para>
/// <para>
/// <b>Neither provider refers to pi.</b> zeta(2n) is a rational multiple of pi^(2n), so a
/// provider that reached it through pi would make <c>pi^(2n)/zeta(2n)</c> exact by construction,
/// and <see cref="EvenZetaControlTests"/> would then be checking arithmetic rather than the
/// bench. One binomial series and one Bernoulli summation, both from reciprocal powers.
/// </para>
/// <para>
/// zeta(6) has no partner here: the central-binomial family stops at s = 4, measured. Its bound
/// is grounded against <see cref="DirectSumZeta"/> and the reference instead, which is weaker
/// evidence than a pair and is described that way.
/// </para>
/// </remarks>
public class EvenZetaCrossCheckTests
{
    /// <summary>How far into the slow provider each grid runs.</summary>
    private const int CentralSteps = 30;

    /// <summary>How far into the fast provider each grid runs.</summary>
    private const int EulerSteps = 12;

    private static Approximation[] CentralPrefix(int order) =>
        [.. new CentralBinomialZeta(order).Refinements().Take(CentralSteps)];

    private static Approximation[] EulerPrefix(int order) =>
        [.. new EulerMaclaurinZeta(order).Refinements().Take(EulerSteps)];

    private static Approximation Displaced(int order, BigRational offset) =>
        new DisplacedConstant(new EulerMaclaurinZeta(order), offset)
            .Refinements().Skip(EulerSteps - 1).First();

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void EveryPairingOfTheTwoProvidersOverlaps(int order)
    {
        Approximation[] central = CentralPrefix(order);
        Approximation[] euler = EulerPrefix(order);

        for (int i = 0; i < central.Length; i++)
        {
            for (int j = 0; j < euler.Length; j++)
            {
                Assert.True(
                    Meet(central[i], euler[j]),
                    Inv($"s={order}: central step {i} and Euler-Maclaurin step {j} claim disjoint enclosures"));
            }
        }
    }

    [Theory]
    [InlineData(2, 7)]
    [InlineData(4, 8)]
    public void EulerMaclaurinSitsInsideTheCentralBinomialOnceItIsTheFinerOfTheTwo(
        int order, int firstContainedStep)
    {
        Approximation[] central = CentralPrefix(order);
        Approximation[] euler = EulerPrefix(order);

        // The threshold is measured, not derived from which bound is numerically finer.
        // Containment also needs the gap between the two values to fit inside the difference of
        // the bounds, and the central binomial's realised error runs at 0.87 to 0.99 of its
        // claim, so it eats most of that difference.
        for (int i = 0; i < central.Length; i++)
        {
            for (int j = firstContainedStep; j < euler.Length; j++)
            {
                Assert.True(
                    Contain(central[i], euler[j]),
                    Inv($"s={order}: Euler-Maclaurin step {j} is not inside central step {i}"));
            }
        }
    }

    [Theory]
    [InlineData(2, 22, 20)]
    [InlineData(4, 25, 23)]
    public void TheTightestPairingsToleranceIsSmall_AndIsNotTheSameInBothDirections(
        int order, int upwardPlaces, int downwardPlaces)
    {
        Approximation central = CentralPrefix(order)[CentralSteps - 1];
        Approximation euler = EulerPrefix(order)[EulerSteps - 1];

        // What the cross-check's tolerance actually is, as two numbers rather than one.
        //
        // Euler-Maclaurin's bound at step 11 is around 1e-35 and contributes almost nothing; the
        // central binomial's at step 29 is the whole tolerance. That series is all-positive, so
        // its partial sum sits BELOW zeta(s) by nearly its whole claimed bound, which is what
        // makes the two thresholds differ by an order or two rather than a few per cent.
        BigRational distance = BigRational.Abs(central.Value - euler.Value);
        BigRational upward = central.MaxError + euler.MaxError - distance;
        BigRational downward = central.MaxError + euler.MaxError + distance;

        Assert.True(upward.Sign > 0, Inv($"the tightest pairing did not overlap at all: {upward}"));

        // s=2: about 1.36e-22 and 1.78e-20, a factor of 131.
        // s=4: about 2.21e-25 and 1.29e-23, a factor of 58.
        Assert.True(upward > TenToTheMinus(upwardPlaces), Inv($"upward tolerance was {upward}"));
        Assert.True(upward < TenToTheMinus(upwardPlaces - 1), Inv($"upward tolerance was {upward}"));
        Assert.True(downward > TenToTheMinus(downwardPlaces), Inv($"downward tolerance was {downward}"));
        Assert.True(downward < TenToTheMinus(downwardPlaces - 1), Inv($"downward tolerance was {downward}"));
        Assert.True(downward > upward * 50);
    }

    [Theory]
    [InlineData(2, 19, 21, 22)]
    [InlineData(4, 22, 24, 25)]
    public void TheCrossCheckRejectsAProviderDisplacedByMoreThanThatTolerance(
        int order, int caughtBothWays, int caughtUpwardOnly, int caughtNeither)
    {
        Approximation central = CentralPrefix(order)[CentralSteps - 1];

        Assert.True(Meet(central, EulerPrefix(order)[EulerSteps - 1]));

        // Caught in both directions. Without an assertion that can fail, the passing ones above
        // would only show that the predicate returns true, not that it can return false.
        Assert.False(
            Meet(central, Displaced(order, TenToTheMinus(caughtBothWays))),
            Inv($"s={order}: a displacement of 1e-{caughtBothWays} above went undetected"));
        Assert.False(
            Meet(central, Displaced(order, -TenToTheMinus(caughtBothWays))),
            Inv($"s={order}: a displacement of 1e-{caughtBothWays} below went undetected"));

        // Caught in one direction only. Asserted rather than avoided: this is the cross-check's
        // real resolution against a provider whose own value is off-centre, and rounding it to a
        // single symmetric number would overstate what the test detects.
        Assert.False(
            Meet(central, Displaced(order, TenToTheMinus(caughtUpwardOnly))),
            Inv($"s={order}: a displacement of 1e-{caughtUpwardOnly} above went undetected"));
        Assert.True(Meet(central, Displaced(order, -TenToTheMinus(caughtUpwardOnly))));

        // Below both thresholds and invisible either way.
        Assert.True(Meet(central, Displaced(order, TenToTheMinus(caughtNeither))));
        Assert.True(Meet(central, Displaced(order, -TenToTheMinus(caughtNeither))));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void TheCrossChecksSensitivityIsBoundedByTheCoarserProvider(int order)
    {
        // The honest limit, asserted rather than left for someone to discover. Against the
        // central binomial's step 0, whose bound is 1/6 or 1/34, the same displacement is
        // invisible: a cross-check can only refute a disagreement larger than the two bounds
        // together.
        Approximation shallow = new CentralBinomialZeta(order).Refinements().First();

        Assert.True(Meet(shallow, Displaced(order, TenToTheMinus(19))));
    }

    [Fact]
    public void ZetaSixHasNoCrossCheckPartner_AndIsGroundedOnTheDefinitionInstead()
    {
        // Said in a test rather than only in prose, because the temptation is to read the even
        // controls as uniformly paired. The central-binomial family has no s = 6 member -
        // zeta(6) over that sum is 2.02385..., not a rational - so what stands behind zeta(6) is
        // Euler-Maclaurin against direct summation, which is a third opinion rather than a
        // second independent method of comparable depth.
        Assert.Throws<ArgumentOutOfRangeException>(() => new CentralBinomialZeta(6));

        Approximation fast = new EulerMaclaurinZeta(6).Refinements().Skip(11).First();
        Approximation slow = new DirectSumZeta(6).Refinements().Skip(199).First();

        Assert.True(Meet(fast, slow), "Euler-Maclaurin and direct summation disagree on zeta(6)");
        Assert.True(Contain(slow, fast), "the fast enclosure is not inside the slow one");
    }
}
