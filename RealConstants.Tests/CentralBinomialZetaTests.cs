using System.Numerics;
using static HalHeinrich.Numerics.Tests.Enclosures;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="CentralBinomialZeta"/> against its own contract, at all three orders the family
/// reaches, with particular attention to the two things its bound rests on: the term ratio
/// staying below a quarter, and each order keeping the tighter of the two bound shapes.
/// </summary>
/// <remarks>
/// The <c>s = 3</c> member is the series Apery used, and these tests inherit the ones written
/// for it when it was its own type. The family's other two members are the even positive
/// controls of <c>SPEC-rational-ratio.md</c> section 4, and neither refers to pi - which is the
/// whole point of them.
/// </remarks>
public class CentralBinomialZetaTests
{
    // ---------- The family's domain ----------

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    public void Constructor_RejectsAnOrderTheFamilyHasNoMemberFor(int order)
    {
        // s = 6 is refused for a measured reason rather than an unimplemented one:
        // zeta(6) / sum 1/(k^6 C(2k,k)) is 2.02385..., which is no rational coefficient. The
        // guard carries that fact, the way NewtonSquareRoot's carries the perfect squares.
        Assert.Throws<ArgumentOutOfRangeException>(() => new CentralBinomialZeta(order));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Order_ReportsTheValueConstructedWith(int order) =>
        Assert.Equal(order, new CentralBinomialZeta(order).Order);

    // ---------- ErrorBoundAt ----------

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ErrorBoundAt_RejectsANegativeStep(int order)
    {
        CentralBinomialZeta zeta = new(order);

        Assert.Throws<ArgumentOutOfRangeException>(() => zeta.ErrorBoundAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => zeta.ErrorBoundAt(int.MinValue));
    }

    [Fact]
    public void ErrorBoundAt_IsTheFirstOmittedTermUnderTheRightShapeForEachOrder()
    {
        // Written out independently of the implementation. At step 0 the first omitted term is
        // k = 2, whose central binomial is C(4,2) = 6.
        //
        // s = 2 does not alternate: 3 * 1/(4*6) * 4/3 = 1/6.
        // s = 3 alternates:       5/2 * 1/(8*6)       = 5/96.
        // s = 4 does not:      36/17 * 1/(16*6) * 4/3 = 1/34.
        //
        // The three differ in both factors, so a coefficient swapped between orders, or the
        // geometric factor applied to the alternating member, fails here.
        Assert.Equal(new BigRational(1, 6), new CentralBinomialZeta(2).ErrorBoundAt(0));
        Assert.Equal(new BigRational(5, 96), new CentralBinomialZeta(3).ErrorBoundAt(0));
        Assert.Equal(new BigRational(1, 34), new CentralBinomialZeta(4).ErrorBoundAt(0));

        // Step 1 omits k = 3, whose central binomial is C(6,3) = 20.
        Assert.Equal(new BigRational(1, 45), new CentralBinomialZeta(2).ErrorBoundAt(1));
        Assert.Equal(new BigRational(1, 216), new CentralBinomialZeta(3).ErrorBoundAt(1));
        Assert.Equal(new BigRational(4, 2295), new CentralBinomialZeta(4).ErrorBoundAt(1));
    }

    [Fact]
    public void TheAlternatingMemberKeepsTheTighterBound()
    {
        // The tightest sound bound each order has earned, rather than one shape imposed on all
        // three for symmetry. Flattening s = 3 to the geometric form would widen its bound by
        // exactly 4/3 at every step - sound, but discarding the alternating-series theorem.
        CentralBinomialZeta alternating = new(3);

        for (int step = 0; step < 20; step++)
        {
            BigRational flattened = alternating.ErrorBoundAt(step) * 4 / 3;

            Assert.True(
                alternating.ErrorBoundAt(step) < flattened,
                Inv($"step {step} did not keep the tighter alternating bound"));
        }
    }

    [Fact]
    public void TheTermRatioStaysBelowAQuarter_WhichIsWhatBothBoundShapesRestOn()
    {
        // One inequality carries the whole family: it makes the terms strictly decreasing (so
        // the alternating estimate applies) and makes them fall at least geometrically (so the
        // positive members' tail is bounded by a/(1 - 1/4)). Checked against the ratio formula
        // directly, not against a provider, and over orders the family does not even use.
        for (int order = 2; order <= 8; order++)
        {
            for (int k = 1; k <= 300; k++)
            {
                BigRational ratio = new(
                    BigInteger.Pow(k, order),
                    BigInteger.Pow(k + 1, order - 1) * 2 * ((2 * k) + 1));

                Assert.True(
                    ratio < new BigRational(1, 4),
                    Inv($"ratio at s={order}, k={k} was {ratio}"));
            }
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ErrorBoundAt_IsStrictlyDecreasing(int order)
    {
        CentralBinomialZeta zeta = new(order);

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
        IRealConstant two = new CentralBinomialZeta(2);
        IRealConstant three = new CentralBinomialZeta(3);
        IRealConstant four = new CentralBinomialZeta(4);

        Assert.Equal(14, two.StepFor(TenToTheMinus(10)));
        Assert.Equal(12, three.StepFor(TenToTheMinus(10)));
        Assert.Equal(10, four.StepFor(TenToTheMinus(10)));

        Assert.Equal(46, two.StepFor(TenToTheMinus(30)));
        Assert.Equal(43, three.StepFor(TenToTheMinus(30)));
        Assert.Equal(40, four.StepFor(TenToTheMinus(30)));

        Assert.True(two.ErrorBoundAt(46) <= TenToTheMinus(30));
        Assert.True(two.ErrorBoundAt(45) > TenToTheMinus(30));
    }

    // ---------- Refinements ----------

    [Fact]
    public void Refinements_MatchTheKnownPartialSums()
    {
        // Step 0 is c_s times the single term 1/(1 * C(2,1)) = 1/2, so 3/2, 5/4 and 18/17.
        // Step 1 adds or subtracts 1/(2^s * 6).
        BigRational[] expectedTwo = [new(3, 2), new(13, 8)];
        BigRational[] expectedThree = [new(5, 4), new(115, 96)];
        BigRational[] expectedFour = [new(18, 17), new(147, 136)];

        BigRational[] two = [.. new CentralBinomialZeta(2).Refinements().Take(2).Select(r => r.Value)];
        BigRational[] three = [.. new CentralBinomialZeta(3).Refinements().Take(2).Select(r => r.Value)];
        BigRational[] four = [.. new CentralBinomialZeta(4).Refinements().Take(2).Select(r => r.Value)];

        Assert.Equal(expectedTwo, two);
        Assert.Equal(expectedThree, three);
        Assert.Equal(expectedFour, four);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Refinements_CarryExactlyTheBoundTheErrorFunctionPromises(int order)
    {
        CentralBinomialZeta zeta = new(order);

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

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Refinements_AreStrictlyImproving(int order)
    {
        CentralBinomialZeta zeta = new(order);

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
    public void Refinements_ApproachFromOneSideOrStraddle_AccordingToTheSeriesSign()
    {
        // The positive members climb toward zeta(s) from below and never overshoot; the
        // alternating member straddles, even steps above and odd below. Pinned because the
        // cross-check's asymmetry is a consequence of it.
        ZetaReference two = ZetaReference.For(2);
        ZetaReference three = ZetaReference.For(3);

        int step = 0;
        foreach (Approximation refinement in new CentralBinomialZeta(2).Refinements().Take(20))
        {
            Assert.True(refinement.Value < two.Lower, Inv($"s=2 step {step} was not below zeta(2)"));
            step++;
        }

        step = 0;
        foreach (Approximation refinement in new CentralBinomialZeta(3).Refinements().Take(20))
        {
            bool above = refinement.Value > three.Upper;
            bool below = refinement.Value < three.Lower;
            Assert.True(
                step % 2 == 0 ? above : below,
                Inv($"s=3 step {step} sits on the wrong side of zeta(3)"));
            step++;
        }
    }

    [Fact]
    public void Refinements_AreIndependentBetweenEnumerations()
    {
        CentralBinomialZeta zeta = new(2);

        Approximation[] first = [.. zeta.Refinements().Take(4)];
        Approximation[] second = [.. zeta.Refinements().Take(4)];

        Assert.Equal(first, second);
    }

    // ---------- The bound, tested by trying to violate it ----------

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Refinements_PinZetaWhileTheirBoundIsWiderThanTheReference(int order)
    {
        CentralBinomialZeta zeta = new(order);
        ZetaReference reference = ZetaReference.For(order);

        // Through step 79 every order's claimed bound is still wider than the reference's
        // sixtieth-place window, so the strong containment is the right assertion. The exact
        // last step at which it holds differs by order - 90, 89 and 84 - and 80 is inside all
        // three.
        int step = 0;
        foreach (Approximation refinement in zeta.Refinements().Take(80))
        {
            Assert.True(
                reference.Pins(refinement),
                Inv($"step {step} claims a bound of {refinement.MaxError} that excludes zeta({order})"));
            step++;
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Refinements_LieWithinTheReferenceOnceFinerThanIt(int order)
    {
        CentralBinomialZeta zeta = new(order);
        ZetaReference reference = ZetaReference.For(order);

        // Step 100's proven bound is between 1.6e-66 and 2.7e-64 depending on the order, some
        // orders of magnitude finer than the reference's last place, so the whole enclosure must
        // sit inside the reference interval. A mistyped digit fails this.
        Approximation deep = zeta.Refinements().Skip(100).First();

        Assert.True(deep.MaxError < TenToTheMinus(63));
        Assert.True(
            reference.LiesWithin(deep),
            Inv($"a 100-step enclosure of half-width {deep.MaxError} fell outside the reference"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Refinements_WouldStopEnclosingZeta_IfTheClaimedBoundWereCutToATenth(int order)
    {
        CentralBinomialZeta zeta = new(order);
        ZetaReference reference = ZetaReference.For(order);

        // This family's bound is the tightest in the repository: measured realised-over-claimed
        // runs 0.80 to 0.99 across these steps, so even a tenth of the claim is refuted at every
        // one of them, in every order. Asserted at a tenth rather than a half because the
        // stronger statement is the true one.
        int step = 0;
        foreach (Approximation refinement in zeta.Refinements().Take(30))
        {
            Approximation shrunk = Approximation.Create(refinement.Value, refinement.MaxError / 10);

            Assert.True(reference.AgreesWith(refinement));
            Assert.False(
                reference.AgreesWith(shrunk),
                Inv($"a tenth-sized bound survived at step {step}"));
            step++;
        }
    }

    [Fact]
    public void TheBoundWouldBeViolated_IfTheIdentitysCoefficientWereDroppedFromIt()
    {
        // The available mistake in the scaling, and it fails immediately at every order. The
        // series sums to zeta(s)/c_s, so a bound derived for the sum and then reported for
        // zeta(s) understates by exactly c_s.
        (int Order, BigRational Coefficient)[] cases =
            [(2, new BigRational(3, 1)), (3, new BigRational(5, 2)), (4, new BigRational(36, 17))];

        foreach ((int order, BigRational coefficient) in cases)
        {
            ZetaReference reference = ZetaReference.For(order);
            int step = 0;

            foreach (Approximation refinement in new CentralBinomialZeta(order).Refinements().Take(25))
            {
                Approximation unscaled =
                    Approximation.Create(refinement.Value, refinement.MaxError / coefficient);

                Assert.True(reference.AgreesWith(refinement));
                Assert.False(
                    reference.AgreesWith(unscaled),
                    Inv($"an unscaled bound survived at s={order}, step {step}"));
                step++;
            }
        }
    }

    [Fact]
    public void TheBoundWouldBeViolated_IfThePositiveMembersDroppedTheGeometricTailFactor()
    {
        // The mistake available only to the non-alternating members: reporting the first omitted
        // term alone, as though the remainder estimate for an alternating series applied. Every
        // later term is added rather than cancelling, so the tail exceeds its first term and the
        // bound fails - at every step tested, in both positive orders.
        foreach (int order in (int[])[2, 4])
        {
            ZetaReference reference = ZetaReference.For(order);
            int step = 0;

            foreach (Approximation refinement in new CentralBinomialZeta(order).Refinements().Take(25))
            {
                Approximation withoutTail =
                    Approximation.Create(refinement.Value, refinement.MaxError * 3 / 4);

                Assert.False(
                    reference.AgreesWith(withoutTail),
                    Inv($"a bound without the geometric tail survived at s={order}, step {step}"));
                step++;
            }
        }
    }
}
