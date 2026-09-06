using static HalHeinrich.Numerics.Tests.Enclosures;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="BorweinZetaThree"/> against its own contract, and against the two mistakes its
/// scalings admit.
/// </summary>
/// <remarks>
/// The bound here is not an alternating-series remainder and it is not tight: it replaces an
/// oscillating integral by the maximum of a polynomial, discarding cancellation that is most of
/// why the scheme converges. That is a permitted direction but it changes what a falsification
/// test can claim, and these tests say so rather than overstating: halving the bound is refuted
/// at step 0 and nowhere deeper, and dropping the <c>4/3</c> from the bound is not refuted at
/// all.
/// </remarks>
public class BorweinZetaThreeTests
{
    // ---------- ErrorBoundAt ----------

    [Fact]
    public void ErrorBoundAt_RejectsANegativeStep()
    {
        BorweinZetaThree zeta = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => zeta.ErrorBoundAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => zeta.ErrorBoundAt(int.MinValue));
    }

    [Fact]
    public void ErrorBoundAt_IsFourThirdsOverTheChebyshevValueAtThree()
    {
        BorweinZetaThree zeta = new();

        // Written out independently of the implementation. T_m(3) runs 3, 17, 99, 577, ... from
        // T_0 = 1, T_1 = 3, T_(m+1) = 6*T_m - T_(m-1), and step n uses degree m = n+1.
        Assert.Equal(new BigRational(4, 9), zeta.ErrorBoundAt(0));
        Assert.Equal(new BigRational(4, 51), zeta.ErrorBoundAt(1));
        Assert.Equal(new BigRational(4, 297), zeta.ErrorBoundAt(2));
        Assert.Equal(new BigRational(4, 1731), zeta.ErrorBoundAt(3));
    }

    [Fact]
    public void ErrorBoundAt_IsStrictlyDecreasing()
    {
        BorweinZetaThree zeta = new();

        BigRational previous = zeta.ErrorBoundAt(0);
        for (int step = 1; step <= 60; step++)
        {
            BigRational current = zeta.ErrorBoundAt(step);

            Assert.True(current < previous, Inv($"step {step} did not improve on {step - 1}"));
            previous = current;
        }
    }

    [Fact]
    public void ErrorBoundAt_TendsToZero_AtRoughlyThreeQuartersOfADigitPerStep()
    {
        IRealConstant zeta = new BorweinZetaThree();

        // log10(3 + sqrt 8) is about 0.766, and the step counts track it.
        Assert.Equal(13, zeta.StepFor(TenToTheMinus(10)));
        Assert.Equal(39, zeta.StepFor(TenToTheMinus(30)));
        Assert.Equal(78, zeta.StepFor(TenToTheMinus(60)));

        Assert.True(zeta.ErrorBoundAt(39) <= TenToTheMinus(30));
        Assert.True(zeta.ErrorBoundAt(38) > TenToTheMinus(30));
    }

    // ---------- Refinements ----------

    [Fact]
    public void Refinements_MatchTheKnownRecombinations()
    {
        BorweinZetaThree zeta = new();

        // Degree 1: P = 2x-1, P(-1) = -3, R = -2, so eta ~ 2/3 and zeta ~ 8/9.
        // Degree 2: P = 8x^2-8x+1, P(-1) = 17, R = 16-8x, so eta ~ (16 - 1/8)/17 = 15/17 and
        // zeta ~ 20/17. Both worked out by hand from the derivation, not read off a run.
        BigRational[] expected = [new(8, 9), new(20, 17), new(9632, 8019)];

        BigRational[] actual = [.. zeta.Refinements().Take(expected.Length).Select(r => r.Value)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Refinements_CarryExactlyTheBoundTheErrorFunctionPromises()
    {
        BorweinZetaThree zeta = new();

        // The two reach the same number by different routes: the refinement divides by the
        // polynomial it built, evaluated at -1, while ErrorBoundAt runs the integer recurrence
        // for T_m(3) and never builds a polynomial at all. Agreeing is evidence for the identity
        // |P_m(-1)| = T_m(3) rather than a tautology.
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
        BorweinZetaThree zeta = new();

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
    public void Refinements_SitBelowZetaThreeForThreeStepsAndAboveItThereafter()
    {
        BorweinZetaThree zeta = new();

        // Unlike an alternating series this one does not straddle: measured over this range the
        // approximation crosses zeta(3) once, at step 3, and stays above. Pinned because the
        // cross-check's asymmetry is a consequence of it - Borwein above and an odd-indexed
        // Apery step below puts the two on opposite sides of the target.
        int step = 0;
        foreach (Approximation refinement in zeta.Refinements().Take(40))
        {
            bool above = refinement.Value > ZetaThreeReference.Upper;
            bool below = refinement.Value < ZetaThreeReference.Lower;

            Assert.True(
                step < 3 ? below : above,
                Inv($"step {step} sits on the unexpected side of zeta(3)"));
            step++;
        }
    }

    [Fact]
    public void Refinements_AreIndependentBetweenEnumerations()
    {
        BorweinZetaThree zeta = new();

        Approximation[] first = [.. zeta.Refinements().Take(4)];
        Approximation[] second = [.. zeta.Refinements().Take(4)];

        Assert.Equal(first, second);
    }

    // ---------- The bound, tested by trying to violate it ----------

    [Fact]
    public void Refinements_PinZetaThreeWhileTheirBoundIsWiderThanTheReference()
    {
        BorweinZetaThree zeta = new();

        // Through step 78 the claimed bound is still wider than the reference's sixtieth-place
        // window, so the strong containment is the right assertion.
        int step = 0;
        foreach (Approximation refinement in zeta.Refinements().Take(79))
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
        BorweinZetaThree zeta = new();

        // Step 90's proven bound is around 5.8e-70, ten orders finer than the reference's last
        // place, so its whole enclosure must sit inside the reference interval. This is the
        // assertion that checks the typed digits against a computation.
        Approximation deep = zeta.Refinements().Skip(90).First();

        Assert.True(deep.MaxError < TenToTheMinus(63));
        Assert.True(
            ZetaThreeReference.LiesWithin(deep),
            Inv($"a 90-step enclosure of half-width {deep.MaxError} fell outside the reference"));
    }

    [Fact]
    public void TheClaimedBoundIsRefutedByHalvingItAtStepZero_AndOnlyThere()
    {
        BorweinZetaThree zeta = new();
        Approximation[] prefix = [.. zeta.Refinements().Take(20)];

        // At step 0 the realised error is about 0.70 of the claimed bound, so halving the claim
        // is refuted at once. By step 1 the realised error is under a third of the claim and
        // halving survives, because this bound discards the cancellation in an oscillating
        // integral and the slack grows with the degree.
        //
        // Asserted in both directions rather than tested only where it passes. A test that
        // checked halving at step 0 and stopped would leave a reader believing the bound is
        // tight throughout, which it is not; and a test that asserted refutation deeper would
        // assert something false.
        Approximation halvedAtZero =
            Approximation.Create(prefix[0].Value, prefix[0].MaxError / 2);

        Assert.True(ZetaThreeReference.AgreesWith(prefix[0]));
        Assert.False(ZetaThreeReference.AgreesWith(halvedAtZero), "a half-sized bound survived at step 0");

        for (int step = 1; step < prefix.Length; step++)
        {
            Approximation halved =
                Approximation.Create(prefix[step].Value, prefix[step].MaxError / 2);

            Assert.True(
                ZetaThreeReference.AgreesWith(halved),
                Inv($"step {step} unexpectedly refuted a half-sized bound"));
        }
    }

    [Fact]
    public void TheBoundWouldBeViolated_IfTheEtaToZetaFactorWereDroppedFromTheValue()
    {
        BorweinZetaThree zeta = new();

        // The scheme computes eta(3), and zeta(3) = eta(3)/(1 - 2^(1-3)) = (4/3)*eta(3).
        // Reporting eta(3) in its place is wrong by a quarter of the answer and fails at every
        // step, immediately.
        int step = 0;
        foreach (Approximation refinement in zeta.Refinements().Take(40))
        {
            Approximation unscaled =
                Approximation.Create(refinement.Value * 3 / 4, refinement.MaxError);

            Assert.True(ZetaThreeReference.AgreesWith(refinement));
            Assert.False(
                ZetaThreeReference.AgreesWith(unscaled),
                Inv($"an unscaled value survived at step {step}"));
            step++;
        }
    }

    [Fact]
    public void TheEtaToZetaFactorIsPresentInTheBound_ThoughDroppingItWouldNotBeCaught()
    {
        BorweinZetaThree zeta = new();

        // The other available mistake, and unlike the one above it does not fail numerically.
        // Applying 4/3 to the value but not to the bound leaves three quarters of the correct
        // bound, and the slack in the derivation covers that at every step tested - at step 0,
        // where the bound is tightest, the realised error is about 0.94 of the reduced bound.
        //
        // So the test checks the factor is *present*, not that its absence would be caught. A
        // bound that holds because a quantity nobody accounted for happened to be covered by
        // slack is a measurement wearing a proof's clothes, which is exactly what this project
        // forbids - and asserting a falsification that does not happen would be worse than
        // asserting nothing.
        int step = 0;
        foreach (Approximation refinement in zeta.Refinements().Take(20))
        {
            BigRational withoutTheFactor = refinement.MaxError * 3 / 4;

            Assert.True(
                refinement.MaxError > withoutTheFactor,
                Inv($"the eta-to-zeta factor is missing from the bound at step {step}"));
            Assert.True(
                ZetaThreeReference.AgreesWith(
                    Approximation.Create(refinement.Value, withoutTheFactor)),
                Inv($"step {step} refuted the reduced bound, so this test can be strengthened"));
            step++;
        }
    }
}
