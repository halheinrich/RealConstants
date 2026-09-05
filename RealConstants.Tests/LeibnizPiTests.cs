using static HalHeinrich.Numerics.Tests.Enclosures;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="LeibnizPi"/> against its own contract: the bound it claims, the values it produces,
/// and an attempt to violate the bound rather than an observation that it held.
/// </summary>
public class LeibnizPiTests
{
    // ---------- ErrorBoundAt ----------

    [Fact]
    public void ErrorBoundAt_RejectsANegativeStep()
    {
        LeibnizPi pi = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => pi.ErrorBoundAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pi.ErrorBoundAt(int.MinValue));
    }

    [Fact]
    public void ErrorBoundAt_IsTheFirstOmittedTermScaledToPi()
    {
        LeibnizPi pi = new();

        // Step n omits the term 1/(2n+3); the four in front of the sum scales it to pi.
        Assert.Equal(Ratio(4, 3), pi.ErrorBoundAt(0));
        Assert.Equal(Ratio(4, 5), pi.ErrorBoundAt(1));
        Assert.Equal(Ratio(4, 7), pi.ErrorBoundAt(2));
        Assert.Equal(Ratio(4, 9), pi.ErrorBoundAt(3));
        Assert.Equal(Ratio(4, 11), pi.ErrorBoundAt(4));
        Assert.Equal(Ratio(4, 2003), pi.ErrorBoundAt(1000));
    }

    [Fact]
    public void ErrorBoundAt_StaysCorrectWhereIntArithmeticWouldOverflow()
    {
        LeibnizPi pi = new();

        // 2*step+3 overflows int above (int.MaxValue-3)/2, and at int.MaxValue it wraps to 1,
        // which would report a bound of 4 - larger than step 0's. A bracketing StepFor probes
        // exactly here for a small target, and would then conclude the series does not converge.
        Assert.Equal(new BigRational(4, 4294967297L), pi.ErrorBoundAt(int.MaxValue));
        Assert.True(
            pi.ErrorBoundAt(int.MaxValue) < pi.ErrorBoundAt(int.MaxValue - 1),
            "the bound must still be decreasing at the top of the int range");
        Assert.True(pi.ErrorBoundAt(int.MaxValue) < pi.ErrorBoundAt(0));
    }

    [Fact]
    public void ErrorBoundAt_IsStrictlyDecreasing()
    {
        LeibnizPi pi = new();

        BigRational previous = pi.ErrorBoundAt(0);
        for (int step = 1; step <= 500; step++)
        {
            BigRational current = pi.ErrorBoundAt(step);
            Assert.True(current < previous, Inv($"step {step} did not improve on {step - 1}"));
            previous = current;
        }
    }

    [Fact]
    public void ErrorBoundAt_TendsToZero_AndTheCrossingIsWhereTheClosedFormPutsIt()
    {
        // StepFor is a default interface member, so it needs an interface-typed reference.
        IRealConstant pi = new LeibnizPi();
        BigRational target = TenToTheMinus(3);

        // 4/(2n+3) <= 1/1000 first at 2n+3 >= 4000, so n = 1999 with 2n+3 = 4001.
        int step = pi.StepFor(target);

        Assert.Equal(1999, step);
        Assert.True(pi.ErrorBoundAt(step) <= target);
        Assert.True(pi.ErrorBoundAt(step - 1) > target);
    }

    [Fact]
    public void ErrorBoundAt_CostsTenTimesTheStepsPerFurtherDecimalPlace()
    {
        // The claim the type's documentation makes about being a control rather than a
        // workhorse, asserted rather than left as prose. Costed through StepFor, which reads
        // the bound alone and computes no refinements at all.
        IRealConstant pi = new LeibnizPi();

        Assert.Equal(1999, pi.StepFor(TenToTheMinus(3)));
        Assert.Equal(19999, pi.StepFor(TenToTheMinus(4)));
        Assert.Equal(199999, pi.StepFor(TenToTheMinus(5)));
    }

    // ---------- Refinements ----------

    [Fact]
    public void Refinements_MatchTheKnownPartialSums()
    {
        LeibnizPi pi = new();

        // 4 * (1), 4 * (1 - 1/3), 4 * (1 - 1/3 + 1/5), and so on, in lowest terms.
        BigRational[] expected =
        [
            Ratio(4, 1),
            Ratio(8, 3),
            Ratio(52, 15),
            Ratio(304, 105),
            Ratio(1052, 315),
        ];

        BigRational[] actual = [.. pi.Refinements().Take(expected.Length).Select(r => r.Value)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Refinements_CarryExactlyTheBoundTheErrorFunctionPromises()
    {
        LeibnizPi pi = new();

        int step = 0;
        foreach (Approximation refinement in pi.Refinements().Take(120))
        {
            Assert.Equal(pi.ErrorBoundAt(step), refinement.MaxError);
            step++;
        }

        Assert.Equal(120, step);
    }

    [Fact]
    public void Refinements_AreStrictlyImproving()
    {
        LeibnizPi pi = new();

        BigRational? previous = null;
        foreach (Approximation refinement in pi.Refinements().Take(200))
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
        LeibnizPi pi = new();

        // Endless and lazy means a caller takes a prefix; two callers must not share position.
        Approximation[] first = [.. pi.Refinements().Take(4)];
        Approximation[] second = [.. pi.Refinements().Take(4)];

        Assert.Equal(first, second);
    }

    // ---------- The bound, tested by trying to violate it ----------

    [Fact]
    public void Refinements_PinPiAtEveryStepTested()
    {
        LeibnizPi pi = new();

        int step = 0;
        foreach (Approximation refinement in pi.Refinements().Take(300))
        {
            Assert.True(
                PiReference.Pins(refinement),
                Inv($"step {step} claims a bound of {refinement.MaxError} that excludes pi"));
            step++;
        }
    }

    [Fact]
    public void Refinements_WouldStopPinningPi_IfTheClaimedBoundWereHalved()
    {
        LeibnizPi pi = new();
        Approximation first = pi.Refinements().First();

        // The bound holding is only worth something if it could have failed. At step 0 the true
        // error is 4 - pi, about 0.858, against a claimed bound of 4/3: more than half of it, so
        // a bound half the size is refuted by the reference outright. The claim is therefore
        // within a factor of two of tight here, not slack enough to hold whatever the value was.
        Approximation halved = Approximation.Create(first.Value, first.MaxError / 2);

        Assert.True(PiReference.AgreesWith(first));
        Assert.False(PiReference.AgreesWith(halved));
    }

    [Fact]
    public void Refinements_PinTheFirstDecimalPlaceAfterTwoHundredTerms()
    {
        LeibnizPi pi = new();

        // Two hundred terms buy one decimal place. That is the control's whole character, and
        // it is asserted here so nobody quietly "improves" it into a second provider of the
        // kind MachinPi already is.
        Approximation refinement = pi.Refinements().Skip(199).First();

        Assert.True(refinement.Lower > Ratio(312, 100), Inv($"lower was {refinement.Lower}"));
        Assert.True(refinement.Upper < Ratio(315, 100), Inv($"upper was {refinement.Upper}"));
        Assert.False(refinement.Lower > Ratio(3141, 1000));
    }
}
