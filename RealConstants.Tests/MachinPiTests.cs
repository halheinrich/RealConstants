using System.Globalization;
using System.Numerics;
using static HalHeinrich.Numerics.Tests.Enclosures;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="MachinPi"/> against its own contract, with particular attention to the two ways its
/// bound can be got wrong: dropping one of the two arctangent series, and dropping the scaling
/// that turns the identity's <c>pi/4</c> into <c>pi</c>.
/// </summary>
public class MachinPiTests
{
    /// <summary>The largest step whose bound this type is willing to compute.</summary>
    private const int MaxBoundableStep = (int.MaxValue - 3) / 2;

    // ---------- ErrorBoundAt ----------

    [Fact]
    public void ErrorBoundAt_RejectsANegativeStep()
    {
        MachinPi pi = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => pi.ErrorBoundAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pi.ErrorBoundAt(int.MinValue));
    }

    [Fact]
    public void ErrorBoundAt_RejectsAStepWhoseExponentWouldNotFitAnInt()
    {
        MachinPi pi = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => pi.ErrorBoundAt(MaxBoundableStep + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pi.ErrorBoundAt(int.MaxValue));

        // Only the rejecting side of this boundary is testable. Accepting MaxBoundableStep means
        // raising 5 to about two billion, which is a number with a billion digits and no test
        // that finishes. The guard's value is in the message it gives rather than in a limit
        // anything will meet; a caller reaching it has already asked for the impossible.
    }

    [Fact]
    public void ErrorBoundAt_IsBothSeriesUnderBothScalings()
    {
        MachinPi pi = new();

        // Written out independently of the implementation. At step 0 the first omitted power is
        // the third, so the bound is 16/(5^3 * 3) + 4/(239^3 * 3) = 16/375 + 4/40955757, which
        // reduces to the literal below. At step 1 the first omitted power is the fifth.
        Assert.Equal(new BigRational(218431204, 5119469625L), pi.ErrorBoundAt(0));
        Assert.Equal(
            new BigRational(
                BigInteger.Parse("12476980255684", CultureInfo.InvariantCulture),
                BigInteger.Parse("12184551018734375", CultureInfo.InvariantCulture)),
            pi.ErrorBoundAt(1));
    }

    [Fact]
    public void ErrorBoundAt_IncludesTheSecondSeriesRatherThanNeglectingIt()
    {
        MachinPi pi = new();

        for (int step = 0; step < 12; step++)
        {
            int firstOmitted = (2 * step) + 3;
            BigRational firstSeriesOnly =
                new(16, BigInteger.Pow(5, firstOmitted) * firstOmitted);

            // Strictly greater, at every step: the arctan(1/239) remainder is in the bound.
            //
            // This is the assertion the evidence supports, and the stronger one is deliberately
            // not made. Dropping the second series does NOT produce a bound that fails against
            // pi over any step tested - the neglected term is around seven orders of magnitude
            // smaller than the one kept, so the shortened expression happens to hold. That is
            // precisely why it is unacceptable: it would be a bound that holds because a
            // quantity nobody bounded turned out to be small, which is measurement wearing a
            // proof's clothes. So the test checks the term is present, not that its absence
            // would be caught.
            Assert.True(
                pi.ErrorBoundAt(step) > firstSeriesOnly,
                Inv($"step {step} bound does not exceed the one-series expression"));
        }
    }

    [Fact]
    public void ErrorBoundAt_WouldBeViolated_IfTheIdentitysFactorOfFourWereDropped()
    {
        MachinPi pi = new();

        // The other available mistake, and unlike the one above this one does fail, immediately
        // and at every step. Machin's identity gives pi/4; a bound derived for the identity and
        // then reported for pi understates by exactly four.
        foreach ((Approximation refinement, int step) in pi.Refinements().Take(12).Select((r, i) => (r, i)))
        {
            Approximation unscaled = Approximation.Create(refinement.Value, refinement.MaxError / 4);

            Assert.True(PiReference.AgreesWith(refinement));
            Assert.False(
                PiReference.AgreesWith(unscaled),
                Inv($"a quarter-sized bound survived at step {step}"));
        }
    }

    [Fact]
    public void ErrorBoundAt_IsStrictlyDecreasing()
    {
        MachinPi pi = new();

        BigRational previous = pi.ErrorBoundAt(0);
        for (int step = 1; step <= 60; step++)
        {
            BigRational current = pi.ErrorBoundAt(step);
            Assert.True(current < previous, Inv($"step {step} did not improve on {step - 1}"));
            previous = current;
        }
    }

    [Fact]
    public void ErrorBoundAt_TendsToZero_AndReachesThirtyPlacesInTwentyStepsOrSo()
    {
        IRealConstant pi = new MachinPi();

        Assert.Equal(20, pi.StepFor(TenToTheMinus(30)));
        Assert.Equal(41, pi.StepFor(TenToTheMinus(60)));
        Assert.True(pi.ErrorBoundAt(20) <= TenToTheMinus(30));
        Assert.True(pi.ErrorBoundAt(19) > TenToTheMinus(30));
    }

    // ---------- Refinements ----------

    [Fact]
    public void Refinements_MatchTheKnownCombinations()
    {
        MachinPi pi = new();

        // Step 0 is 4 * (4 * 1/5 - 1/239) = 3804/1195. Step 1 subtracts the cubic term of each
        // series before the same combination is taken.
        BigRational[] expected =
        [
            new BigRational(3804, 1195),
            new BigRational(
                BigInteger.Parse("5359397032", CultureInfo.InvariantCulture),
                BigInteger.Parse("1706489875", CultureInfo.InvariantCulture)),
        ];

        BigRational[] actual = [.. pi.Refinements().Take(expected.Length).Select(r => r.Value)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Refinements_CarryExactlyTheBoundTheErrorFunctionPromises()
    {
        MachinPi pi = new();

        // The two are computed by different routes on purpose - ErrorBoundAt exponentiates,
        // Refinements advances a running power - so agreeing is evidence rather than tautology.
        int step = 0;
        foreach (Approximation refinement in pi.Refinements().Take(60))
        {
            Assert.Equal(pi.ErrorBoundAt(step), refinement.MaxError);
            step++;
        }

        Assert.Equal(60, step);
    }

    [Fact]
    public void Refinements_AreStrictlyImproving()
    {
        MachinPi pi = new();

        BigRational? previous = null;
        foreach (Approximation refinement in pi.Refinements().Take(60))
        {
            if (previous is BigRational last)
            {
                Assert.True(refinement.MaxError < last, "MaxError must strictly decrease");
            }

            previous = refinement.MaxError;
        }
    }

    [Fact]
    public void Refinements_AreIndependentBetweenEnumerations()
    {
        MachinPi pi = new();

        Approximation[] first = [.. pi.Refinements().Take(4)];
        Approximation[] second = [.. pi.Refinements().Take(4)];

        Assert.Equal(first, second);
    }

    // ---------- The bound, tested by trying to violate it ----------

    [Fact]
    public void Refinements_PinPiWhileTheirBoundIsWiderThanTheReference()
    {
        MachinPi pi = new();

        // Up to step 40 the claimed bound is still wider than the reference's own sixtieth-place
        // window, so the strong containment is the right assertion. Past that the enclosure is
        // finer than the reference and the direction reverses; see the next test.
        int step = 0;
        foreach (Approximation refinement in pi.Refinements().Take(41))
        {
            Assert.True(
                PiReference.Pins(refinement),
                Inv($"step {step} claims a bound of {refinement.MaxError} that excludes pi"));
            step++;
        }
    }

    [Fact]
    public void Refinements_AgreeWithTheReferenceAtEveryStepTested()
    {
        MachinPi pi = new();

        // The weak form, which applies at any width. A failure here refutes the bound outright.
        foreach (Approximation refinement in pi.Refinements().Take(70))
        {
            Assert.True(PiReference.AgreesWith(refinement));
        }
    }

    [Fact]
    public void Refinements_WouldStopAgreeingWithPi_IfTheClaimedBoundWereHalved()
    {
        MachinPi pi = new();
        Approximation first = pi.Refinements().First();

        // At step 0 the realised error is about 0.9766 of the claimed bound, so halving the
        // claim is refuted at once. The bound is close to tight where it is cheapest to check,
        // which is what makes the enclosure tests above capable of failing.
        Approximation halved = Approximation.Create(first.Value, first.MaxError / 2);

        Assert.True(PiReference.AgreesWith(first));
        Assert.False(PiReference.AgreesWith(halved));
    }

    // ---------- The reference itself ----------

    [Fact]
    public void TheReferenceDigitsAreConfirmedByAComputationFinerThanTheyAre()
    {
        MachinPi pi = new();

        // PiReference is a hardcoded digit string, and this is what keeps it from being taken on
        // trust. Step 45's proven bound is around 1.7e-66, six orders finer than the reference's
        // last place, so its whole enclosure must sit inside the reference interval. A single
        // mistyped digit anywhere in the sixty moves that interval by at least 1e-60 and fails
        // this assertion.
        Approximation deep = pi.Refinements().Skip(45).First();

        Assert.True(deep.MaxError < TenToTheMinus(63));
        Assert.True(
            PiReference.LiesWithin(deep),
            Inv($"a 45-step enclosure of half-width {deep.MaxError} fell outside the reference"));
    }
}
