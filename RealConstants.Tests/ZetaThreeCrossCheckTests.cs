using static HalHeinrich.Numerics.Tests.Enclosures;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The provider cross-check for zeta(3): <see cref="AperyZetaThree"/> and
/// <see cref="BorweinZetaThree"/> compute it by schemes that share no code, so their enclosures
/// must overlap.
/// </summary>
/// <remarks>
/// <para>
/// This is the strongest correctness test available here, and the reason the pair was built in
/// one session rather than one after the other. Neither provider can check itself, and a
/// hardcoded digit string checks only what someone typed. Two independent computations
/// disagreeing outside their bounds refutes at least one of them without needing to be told
/// which.
/// </para>
/// <para>
/// It is not a substitute for the enclosure tests against <see cref="ZetaThreeReference"/>. The
/// cross-check would be equally happy if both providers converged, in agreement, on some number
/// that is not zeta(3). Together the two kinds of test close that gap; separately neither does.
/// </para>
/// <para>
/// <b>This pair is matched in depth where the pi pair is not</b>, and that reverses which of the
/// two assertions carries the weight. Apery gains about 0.64 decimal digits a step and Borwein
/// about 0.77, so at equal steps their bounds stay within a couple of orders of each other:
/// overlap is a sharp test at every pairing, and containment only becomes available deep in the
/// grid. Against pi, where Machin buries Leibniz within two steps, it is the other way round.
/// </para>
/// </remarks>
public class ZetaThreeCrossCheckTests
{
    /// <summary>How far into Apery the grid runs.</summary>
    private const int AperySteps = 30;

    /// <summary>How far into Borwein the grid runs.</summary>
    private const int BorweinSteps = 40;

    /// <summary>
    /// The first Borwein step whose enclosure sits inside every Apery enclosure in the grid.
    /// Measured, not guessed: below it the two bounds are close enough that containment fails
    /// while overlap still holds.
    /// </summary>
    private const int FirstContainedBorweinStep = 29;

    private static Approximation[] AperyPrefix() =>
        [.. new AperyZetaThree().Refinements().Take(AperySteps)];

    private static Approximation[] BorweinPrefix() =>
        [.. new BorweinZetaThree().Refinements().Take(BorweinSteps)];

    /// <summary>Borwein at its deepest grid step, moved off zeta(3) with its bound left alone.</summary>
    private static Approximation Displaced(BigRational offset) =>
        new DisplacedConstant(new BorweinZetaThree(), offset)
            .Refinements().Skip(BorweinSteps - 1).First();

    [Fact]
    public void EveryPairingOfTheTwoProvidersOverlaps()
    {
        Approximation[] apery = AperyPrefix();
        Approximation[] borwein = BorweinPrefix();

        for (int i = 0; i < apery.Length; i++)
        {
            for (int j = 0; j < borwein.Length; j++)
            {
                Assert.True(
                    Meet(apery[i], borwein[j]),
                    Inv($"Apery step {i} and Borwein step {j} claim disjoint enclosures"));
            }
        }
    }

    [Fact]
    public void BorweinsEnclosureSitsInsideAperysOnceBorweinIsTheFinerOfTheTwo()
    {
        Approximation[] apery = AperyPrefix();
        Approximation[] borwein = BorweinPrefix();

        // Containment needs more than "the inner bound is smaller": it needs the gap between the
        // two values to fit inside the difference of the bounds, and Apery's realised error runs
        // at about 0.81 of its claim. So the threshold is measured rather than derived from
        // which bound is numerically finer - Borwein's bound undercuts Apery's from step 23, but
        // containment against the whole grid only holds from step 29.
        for (int i = 0; i < apery.Length; i++)
        {
            for (int j = FirstContainedBorweinStep; j < borwein.Length; j++)
            {
                Assert.True(
                    Contain(apery[i], borwein[j]),
                    Inv($"Borwein step {j} is not inside Apery step {i}"));
            }
        }
    }

    [Fact]
    public void TheTightestPairingsToleranceIsSmall_AndIsNotTheSameInBothDirections()
    {
        Approximation apery = AperyPrefix()[AperySteps - 1];
        Approximation borwein = BorweinPrefix()[BorweinSteps - 1];

        // What the cross-check's tolerance actually is, as two numbers rather than one.
        //
        // Borwein's bound here is about 6.4e-31 and contributes almost nothing; Apery's is about
        // 1.8e-22 and is the whole tolerance. Step 29 is an odd step, so its value sits BELOW
        // zeta(3) by its realised error of about 1.5e-22, while Borwein sits above. The two are
        // therefore on opposite sides of the target, and displacing Borwein further up has only
        // (sum of bounds - distance) to cross before the intervals part, where displacing it
        // down has (sum of bounds + distance).
        BigRational distance = BigRational.Abs(apery.Value - borwein.Value);
        BigRational upward = apery.MaxError + borwein.MaxError - distance;
        BigRational downward = apery.MaxError + borwein.MaxError + distance;

        Assert.True(upward.Sign > 0, Inv($"the tightest pairing did not overlap at all: {upward}"));

        // About 3.38e-23 and 3.27e-22: the two differ by a factor of about 9.7, so quoting one
        // symmetric tolerance would overstate what this test detects by nearly an order.
        Assert.True(upward > TenToTheMinus(23), Inv($"upward tolerance was {upward}"));
        Assert.True(upward < TenToTheMinus(22), Inv($"upward tolerance was {upward}"));
        Assert.True(downward > TenToTheMinus(22), Inv($"downward tolerance was {downward}"));
        Assert.True(downward < TenToTheMinus(21), Inv($"downward tolerance was {downward}"));
        Assert.True(downward > upward * 9);
    }

    [Fact]
    public void TheCrossCheckRejectsAProviderDisplacedByMoreThanThatTolerance()
    {
        Approximation apery = AperyPrefix()[AperySteps - 1];
        Approximation borwein = BorweinPrefix()[BorweinSteps - 1];

        Assert.True(Meet(apery, borwein));

        // 1e-21 clears both thresholds measured above, so it is caught either way. Without an
        // assertion that can fail, the passing ones above would only show that the predicate
        // returns true, not that it is capable of returning false.
        Assert.False(
            Meet(apery, Displaced(TenToTheMinus(21))),
            "a displacement of 1e-21 above went undetected");
        Assert.False(
            Meet(apery, Displaced(-TenToTheMinus(21))),
            "a displacement of 1e-21 below went undetected");

        // 1e-22 clears the upward threshold but not the downward one, so it is caught in one
        // direction only. Asserted rather than avoided: this is the cross-check's real
        // resolution against a provider whose own value is off-centre, and rounding it to a
        // single symmetric number would overstate what the test detects.
        Assert.False(
            Meet(apery, Displaced(TenToTheMinus(22))),
            "a displacement of 1e-22 above went undetected");
        Assert.True(Meet(apery, Displaced(-TenToTheMinus(22))));

        // 1e-23 is below both thresholds and is invisible in either direction.
        Assert.True(Meet(apery, Displaced(TenToTheMinus(23))));
        Assert.True(Meet(apery, Displaced(-TenToTheMinus(23))));
    }

    [Fact]
    public void TheCrossChecksSensitivityIsBoundedByTheCoarserProvider()
    {
        // The honest limit of the previous test, asserted rather than left for someone to
        // discover. Against Apery step 0, whose bound is 5/96, the same displacement is
        // invisible: a cross-check can only refute a disagreement larger than the two bounds
        // together, so pairing a deep provider with a shallow one proves very little.
        Approximation shallow = new AperyZetaThree().Refinements().First();

        Assert.True(Meet(shallow, Displaced(TenToTheMinus(21))));
    }
}
