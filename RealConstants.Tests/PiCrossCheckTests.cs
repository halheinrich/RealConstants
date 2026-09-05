using static HalHeinrich.Numerics.Tests.Enclosures;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The provider cross-check: <see cref="MachinPi"/> and <see cref="LeibnizPi"/> compute pi by
/// series that share no code, so their enclosures must overlap.
/// </summary>
/// <remarks>
/// <para>
/// This is the strongest correctness test available here, and the reason the pair was built
/// together rather than one after the other. Neither provider can check itself, and a hardcoded
/// digit string checks only what someone typed. Two independent computations disagreeing outside
/// their bounds refutes at least one of them without needing to be told which.
/// </para>
/// <para>
/// It is not a substitute for the enclosure tests against <see cref="PiReference"/>. The
/// cross-check would be equally happy if both providers converged, in agreement, on some number
/// that is not pi. Together the two kinds of test close that gap; separately neither does.
/// </para>
/// </remarks>
public class PiCrossCheckTests
{
    /// <summary>How far into the slow provider the grid runs.</summary>
    private const int LeibnizSteps = 200;

    /// <summary>How far into the fast provider the grid runs.</summary>
    private const int MachinSteps = 25;

    private static Approximation[] LeibnizPrefix() => [.. new LeibnizPi().Refinements().Take(LeibnizSteps)];

    private static Approximation[] MachinPrefix() => [.. new MachinPi().Refinements().Take(MachinSteps)];

    /// <summary>Machin at step 20, moved off pi by the given amount with its bound left alone.</summary>
    private static Approximation Displaced(BigRational offset) =>
        new SkewedPi(offset).Refinements().Skip(20).First();

    [Fact]
    public void EveryPairingOfTheTwoProvidersOverlaps()
    {
        Approximation[] leibniz = LeibnizPrefix();
        Approximation[] machin = MachinPrefix();

        for (int i = 0; i < leibniz.Length; i++)
        {
            for (int j = 0; j < machin.Length; j++)
            {
                Assert.True(
                    Meet(leibniz[i], machin[j]),
                    Inv($"Leibniz step {i} and Machin step {j} claim disjoint enclosures"));
            }
        }
    }

    [Fact]
    public void MachinsEnclosureSitsInsideLeibnizsOnceMachinIsTheFinerOfTheTwo()
    {
        Approximation[] leibniz = LeibnizPrefix();
        Approximation[] machin = MachinPrefix();

        // From Machin step 1 its bound is below 1/1000, finer than every Leibniz bound in this
        // grid, so the strictly stronger containment holds and is what gets asserted. Machin
        // step 0 is excluded because its bound of about 0.0427 is wider than Leibniz's from
        // step 46 onward - overlap still holds there, containment cannot, and asserting it
        // would be asserting something false rather than something strict.
        for (int i = 0; i < leibniz.Length; i++)
        {
            for (int j = 1; j < machin.Length; j++)
            {
                Assert.True(
                    Contain(leibniz[i], machin[j]),
                    Inv($"Machin step {j} is not inside Leibniz step {i}"));
            }
        }
    }

    [Fact]
    public void TheTightestPairingsToleranceIsSmall_AndIsNotTheSameInBothDirections()
    {
        Approximation leibniz = new LeibnizPi().Refinements().Skip(199).First();
        Approximation machin = new MachinPi().Refinements().Skip(20).First();

        // What the cross-check's tolerance actually is, as two numbers rather than one, because
        // it is not symmetric and treating it as symmetric is how the first version of the test
        // below passed in one direction and failed in the other.
        //
        // Machin's bound here is about 3.3e-31 and contributes nothing. Leibniz's is 4/401, and
        // step 199 is an odd step, so its value sits BELOW pi by its realised error of about
        // 5.0e-3. Displacing Machin upward therefore has only (bound - realised error) to cross
        // before the intervals part; displacing it downward has (bound + realised error). The
        // two thresholds are the sum of the bounds minus, and plus, the distance between the
        // values.
        BigRational distance = BigRational.Abs(leibniz.Value - machin.Value);
        BigRational upward = leibniz.MaxError + machin.MaxError - distance;
        BigRational downward = leibniz.MaxError + machin.MaxError + distance;

        Assert.True(upward.Sign > 0, Inv($"the tightest pairing did not overlap at all: {upward}"));

        // About 4.975e-3 and 1.4975e-2 respectively.
        Assert.True(upward > Ratio(1, 250), Inv($"upward tolerance was {upward}"));
        Assert.True(upward < Ratio(1, 200), Inv($"upward tolerance was {upward}"));
        Assert.True(downward > Ratio(1, 70), Inv($"downward tolerance was {downward}"));
        Assert.True(downward < Ratio(1, 66), Inv($"downward tolerance was {downward}"));
        Assert.True(downward > upward);
    }

    [Fact]
    public void TheCrossCheckRejectsAProviderDisplacedByMoreThanThatTolerance()
    {
        Approximation leibniz = new LeibnizPi().Refinements().Skip(199).First();
        Approximation machin = new MachinPi().Refinements().Skip(20).First();

        Assert.True(Meet(leibniz, machin));

        // A fiftieth clears both thresholds measured above, so it is caught either way. Without
        // an assertion that can fail, the passing ones above would only show that the predicate
        // returns true, not that it is capable of returning false.
        Assert.False(
            Meet(leibniz, Displaced(Ratio(1, 50))),
            "a displacement of 1/50 above went undetected");
        Assert.False(
            Meet(leibniz, Displaced(-Ratio(1, 50))),
            "a displacement of 1/50 below went undetected");

        // A hundredth clears the upward threshold but not the downward one, so it is caught in
        // one direction only. Asserted rather than avoided: this is the cross-check's real
        // resolution against a provider whose own value is off-centre, and rounding it to a
        // single symmetric number would overstate what the test detects.
        Assert.False(
            Meet(leibniz, Displaced(Ratio(1, 100))),
            "a displacement of 1/100 above went undetected");
        Assert.True(Meet(leibniz, Displaced(-Ratio(1, 100))));
    }

    [Fact]
    public void TheCrossChecksSensitivityIsBoundedByTheCoarserProvider()
    {
        // The honest limit of the previous test, asserted rather than left for someone to
        // discover. Against Leibniz step 0, whose bound is 4/3, the same displacement is
        // invisible: a cross-check can only refute a disagreement larger than the two bounds
        // together, so pairing a deep provider with a shallow one proves very little.
        Approximation shallow = new LeibnizPi().Refinements().First();

        Assert.True(Meet(shallow, Displaced(Ratio(1, 50))));
    }
}
