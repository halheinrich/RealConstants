using System.Numerics;
using static HalHeinrich.Numerics.Tests.Enclosures;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="AperyZetaThree"/> against its own contract, with particular attention to the two
/// things its bound rests on: that the terms are positive and strictly decreasing, and that the
/// <c>5/2</c> in front of the series reaches the bound as well as the value.
/// </summary>
public class AperyZetaThreeTests
{
    // ---------- ErrorBoundAt ----------

    [Fact]
    public void ErrorBoundAt_RejectsANegativeStep()
    {
        AperyZetaThree zeta = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => zeta.ErrorBoundAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => zeta.ErrorBoundAt(int.MinValue));
    }

    [Fact]
    public void ErrorBoundAt_IsTheFirstOmittedTermUnderTheIdentitysScaling()
    {
        AperyZetaThree zeta = new();

        // Written out independently of the implementation. At step 0 the first omitted term is
        // k = 2, so the bound is (5/2)/(2^3 * C(4,2)) = (5/2)/48 = 5/96. At step 1 it is k = 3,
        // giving (5/2)/(27 * 20) = 1/216, and at step 2 it is k = 4, giving (5/2)/(64*70).
        Assert.Equal(new BigRational(5, 96), zeta.ErrorBoundAt(0));
        Assert.Equal(new BigRational(1, 216), zeta.ErrorBoundAt(1));
        Assert.Equal(new BigRational(1, 1792), zeta.ErrorBoundAt(2));
    }

    [Fact]
    public void TheTermsArePositiveAndStrictlyDecreasing_WhichIsWhatTheBoundRestsOn()
    {
        // The alternating-series remainder estimate is not available for a series whose terms do
        // not strictly decrease to zero, so this is the hypothesis rather than a nicety. Checked
        // directly against the term formula, not against the provider.
        BigRational previous = BigRational.Zero;
        BigInteger central = 2;

        for (int k = 1; k <= 200; k++)
        {
            BigInteger index = k;
            BigRational term = new(BigInteger.One, index * index * index * central);

            Assert.True(term.Sign > 0, Inv($"term {k} is not positive"));
            if (k > 1)
            {
                Assert.True(term < previous, Inv($"term {k} did not fall below term {k - 1}"));
            }

            previous = term;
            central = central * 2 * ((2 * index) + 1) / (index + BigInteger.One);
        }
    }

    [Fact]
    public void ErrorBoundAt_IsStrictlyDecreasing()
    {
        AperyZetaThree zeta = new();

        BigRational previous = zeta.ErrorBoundAt(0);
        for (int step = 1; step <= 60; step++)
        {
            BigRational current = zeta.ErrorBoundAt(step);

            Assert.True(current < previous, Inv($"step {step} did not improve on {step - 1}"));
            previous = current;
        }
    }

    [Fact]
    public void ErrorBoundAt_TendsToZero_AtRoughlySixTenthsOfADigitPerStep()
    {
        IRealConstant zeta = new AperyZetaThree();

        Assert.Equal(12, zeta.StepFor(TenToTheMinus(10)));
        Assert.Equal(43, zeta.StepFor(TenToTheMinus(30)));
        Assert.Equal(91, zeta.StepFor(TenToTheMinus(60)));

        Assert.True(zeta.ErrorBoundAt(43) <= TenToTheMinus(30));
        Assert.True(zeta.ErrorBoundAt(42) > TenToTheMinus(30));
    }

    // ---------- Refinements ----------

    [Fact]
    public void Refinements_MatchTheKnownPartialSums()
    {
        AperyZetaThree zeta = new();

        // Step 0 is (5/2)*(1/2) = 5/4. Step 1 subtracts 1/(2^3*C(4,2)) = 1/48 first, giving
        // (5/2)*(23/48) = 115/96; step 2 adds 1/(3^3*C(6,3)) = 1/540.
        BigRational[] expected = [new(5, 4), new(115, 96), new(1039, 864)];

        BigRational[] actual = [.. zeta.Refinements().Take(expected.Length).Select(r => r.Value)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Refinements_CarryExactlyTheBoundTheErrorFunctionPromises()
    {
        AperyZetaThree zeta = new();

        // The two are computed by different routes on purpose - ErrorBoundAt builds the central
        // binomial coefficient from its product formula, Refinements advances it by its
        // recurrence - so agreeing is evidence rather than tautology.
        int step = 0;
        foreach (Approximation refinement in zeta.Refinements().Take(45))
        {
            Assert.Equal(zeta.ErrorBoundAt(step), refinement.MaxError);
            step++;
        }

        Assert.Equal(45, step);
    }

    [Fact]
    public void Refinements_AreStrictlyImproving()
    {
        AperyZetaThree zeta = new();

        BigRational? previous = null;
        foreach (Approximation refinement in zeta.Refinements().Take(45))
        {
            if (previous is BigRational last)
            {
                Assert.True(refinement.MaxError < last, "MaxError must strictly decrease");
            }

            previous = refinement.MaxError;
        }
    }

    [Fact]
    public void Refinements_StraddleZetaThree_AsAnAlternatingSeriesMust()
    {
        AperyZetaThree zeta = new();

        // Even steps end on an added term and sit above zeta(3); odd steps end on a subtracted
        // one and sit below. Asserted because the cross-check's asymmetry is a consequence of
        // it: an Apery step of odd index is off-centre in a known direction.
        int step = 0;
        foreach (Approximation refinement in zeta.Refinements().Take(20))
        {
            bool above = refinement.Value > ZetaThreeReference.Upper;
            bool below = refinement.Value < ZetaThreeReference.Lower;

            Assert.True(
                step % 2 == 0 ? above : below,
                Inv($"step {step} sits on the wrong side of zeta(3)"));
            step++;
        }
    }

    [Fact]
    public void Refinements_AreIndependentBetweenEnumerations()
    {
        AperyZetaThree zeta = new();

        Approximation[] first = [.. zeta.Refinements().Take(4)];
        Approximation[] second = [.. zeta.Refinements().Take(4)];

        Assert.Equal(first, second);
    }

    // ---------- The bound, tested by trying to violate it ----------

    [Fact]
    public void Refinements_PinZetaThreeWhileTheirBoundIsWiderThanTheReference()
    {
        AperyZetaThree zeta = new();

        // Through step 89 the claimed bound is still wider than the reference's sixtieth-place
        // window, so the strong containment is the right assertion.
        int step = 0;
        foreach (Approximation refinement in zeta.Refinements().Take(90))
        {
            Assert.True(
                ZetaThreeReference.Pins(refinement),
                Inv($"step {step} claims a bound of {refinement.MaxError} that excludes zeta(3)"));
            step++;
        }
    }

    [Fact]
    public void Refinements_LieWithinTheReferenceOnceFinerThanIt()
    {
        AperyZetaThree zeta = new();

        // Step 100's proven bound is around 1.6e-66, six orders finer than the reference's last
        // place, so its whole enclosure must sit inside the reference interval. A single
        // mistyped digit anywhere in the sixty moves that interval by at least 1e-60 and fails
        // this assertion, which is what keeps the digit string from being taken on trust.
        Approximation deep = zeta.Refinements().Skip(100).First();

        Assert.True(deep.MaxError < TenToTheMinus(63));
        Assert.True(
            ZetaThreeReference.LiesWithin(deep),
            Inv($"a 100-step enclosure of half-width {deep.MaxError} fell outside the reference"));
    }

    [Fact]
    public void Refinements_WouldStopEnclosingZetaThree_IfTheClaimedBoundWereHalved()
    {
        AperyZetaThree zeta = new();

        // The realised error runs between about 0.92 and 0.81 of the claimed bound over this
        // range, so halving the claim is refuted at every step in it. The bound is close to
        // tight, which is what makes the enclosure tests above capable of failing.
        int step = 0;
        foreach (Approximation refinement in zeta.Refinements().Take(30))
        {
            Approximation halved = Approximation.Create(refinement.Value, refinement.MaxError / 2);

            Assert.True(ZetaThreeReference.AgreesWith(refinement));
            Assert.False(
                ZetaThreeReference.AgreesWith(halved),
                Inv($"a half-sized bound survived at step {step}"));
            step++;
        }
    }

    [Fact]
    public void TheBoundWouldBeViolated_IfTheIdentitysFiveHalvesWereDroppedFromIt()
    {
        AperyZetaThree zeta = new();

        // The available mistake in the scaling, and it does fail - immediately and at every
        // step. The series sums to (2/5)*zeta(3), so a bound derived for the sum and then
        // reported for zeta(3) understates by exactly 5/2.
        int step = 0;
        foreach (Approximation refinement in zeta.Refinements().Take(30))
        {
            Approximation unscaled =
                Approximation.Create(refinement.Value, refinement.MaxError * 2 / 5);

            Assert.True(ZetaThreeReference.AgreesWith(refinement));
            Assert.False(
                ZetaThreeReference.AgreesWith(unscaled),
                Inv($"an unscaled bound survived at step {step}"));
            step++;
        }
    }
}
