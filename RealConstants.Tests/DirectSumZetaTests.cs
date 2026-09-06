using System.Numerics;
using static HalHeinrich.Numerics.Tests.Enclosures;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="DirectSumZeta"/> against its own contract. It is the obviously-correct slow
/// implementation, so these tests are also the audit that its being obviously correct is worth
/// anything.
/// </summary>
/// <remarks>
/// Its bound is the only one in the repository proved from nothing but the monotonicity of
/// <c>x^-s</c>, and its convergence is the worst by orders of magnitude. Both are the point.
/// </remarks>
public class DirectSumZetaTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-4)]
    public void Constructor_RejectsAnOrderBelowTwo(int order) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new DirectSumZeta(order));

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    public void Order_ReportsTheValueConstructedWith(int order) =>
        Assert.Equal(order, new DirectSumZeta(order).Order);

    [Fact]
    public void ErrorBoundAt_RejectsANegativeStep()
    {
        DirectSumZeta zeta = new(2);

        Assert.Throws<ArgumentOutOfRangeException>(() => zeta.ErrorBoundAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => zeta.ErrorBoundAt(int.MinValue));
    }

    [Fact]
    public void RefinementsAndTheBoundAreTheTailBracket()
    {
        // Step 0 sums one term. For s = 2 the tail lies between the integral of x^-2 from 2
        // (which is 1/2) and from 1 (which is 1), so zeta(2) is in [3/2, 2]: the midpoint 7/4
        // with half-width 1/4. Worked out by hand from the two integrals.
        Approximation first = new DirectSumZeta(2).Refinements().First();

        Assert.Equal(new BigRational(7, 4), first.Value);
        Assert.Equal(new BigRational(1, 4), first.MaxError);

        // For s = 6 at step 0: tail between 1/(5*2^5) = 1/160 and 1/5, so [1 + 1/160, 1 + 1/5].
        Approximation six = new DirectSumZeta(6).Refinements().First();

        Assert.Equal(new BigRational(353, 320), six.Value);
        Assert.Equal(new BigRational(31, 320), six.MaxError);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Refinements_CarryExactlyTheBoundTheErrorFunctionPromises(int order)
    {
        DirectSumZeta zeta = new(order);

        int step = 0;
        foreach (Approximation refinement in zeta.Refinements().Take(60))
        {
            Assert.Equal(zeta.ErrorBoundAt(step), refinement.MaxError);
            step++;
        }

        Assert.Equal(60, step);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void ErrorBoundAt_IsStrictlyDecreasing(int order)
    {
        DirectSumZeta zeta = new(order);

        BigRational previous = zeta.ErrorBoundAt(0);
        for (int step = 1; step <= 200; step++)
        {
            BigRational current = zeta.ErrorBoundAt(step);

            Assert.True(current < previous, Inv($"step {step} did not improve on {step - 1}"));
            previous = current;
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Refinements_AreStrictlyImproving(int order)
    {
        DirectSumZeta zeta = new(order);

        BigRational? previous = null;
        foreach (Approximation refinement in zeta.Refinements().Take(60))
        {
            if (previous is BigRational last)
            {
                Assert.True(refinement.MaxError < last, "MaxError must strictly decrease");
            }

            previous = refinement.MaxError;
        }
    }

    [Fact]
    public void ErrorBoundAt_TendsToZero_ButHopelesslySlowly()
    {
        IRealConstant six = new DirectSumZeta(6);
        IRealConstant four = new DirectSumZeta(4);

        // The bound falls like N^-s, so a decimal digit costs a factor of ten in N for s = 2 and
        // a tenth of that for s = 6. Asserted as step counts, which are deterministic; how long
        // those steps take is an experiment's business, not a test's.
        Assert.Equal(40, six.StepFor(TenToTheMinus(10)));
        Assert.Equal(265, four.StepFor(TenToTheMinus(10)));

        // Ten digits of zeta(2) would want something past a hundred thousand terms, and sixty
        // digits is out of reach at any scale. That is the honest character of the method, and
        // the reason it is the reference rather than the workhorse.
        Assert.True(new DirectSumZeta(2).ErrorBoundAt(100_000) > TenToTheMinus(11));
    }

    [Fact]
    public void Refinements_AreIndependentBetweenEnumerations()
    {
        DirectSumZeta zeta = new(4);

        Approximation[] first = [.. zeta.Refinements().Take(4)];
        Approximation[] second = [.. zeta.Refinements().Take(4)];

        Assert.Equal(first, second);
    }

    // ---------- The bound, tested by trying to violate it ----------

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public void Refinements_PinZetaAtEveryStepTested(int order)
    {
        DirectSumZeta zeta = new(order);
        ZetaReference reference = ZetaReference.For(order);

        // This provider's bound never gets near the reference's sixtieth place, so the strong
        // containment is the only assertion available and it holds throughout.
        int step = 0;
        foreach (Approximation refinement in zeta.Refinements().Take(200))
        {
            Assert.True(
                reference.Pins(refinement),
                Inv($"step {step} claims a bound of {refinement.MaxError} that excludes zeta({order})"));
            step++;
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void ThePartialSumAloneLiesOutsideItsOwnEnclosure_WhichIsWhatTheTailBuys(int order)
    {
        DirectSumZeta zeta = new(order);

        // The falsification that bites at every step, in every order. Drop the tail estimate and
        // keep the bound, and the value falls below the enclosure it claims - the partial sum
        // always undershoots zeta(s) by more than half the bracket width, because the whole
        // bracket lies above it.
        BigRational partial = BigRational.Zero;
        int step = 0;

        foreach (Approximation refinement in zeta.Refinements().Take(60))
        {
            BigInteger term = step + 1;
            partial += new BigRational(BigInteger.One, BigInteger.Pow(term, order));

            Assert.False(
                refinement.Contains(partial),
                Inv($"the bare partial sum was inside its own enclosure at s={order}, step {step}"));
            step++;
        }
    }

    [Fact]
    public void HalvingTheBoundIsRefutedOnlyWhereTheBracketIsCoarse()
    {
        ZetaReference six = ZetaReference.For(6);
        Approximation[] prefix = [.. new DirectSumZeta(6).Refinements().Take(8)];

        // The true tail sits near the middle of the bracket rather than at an end, so the
        // half-width overstates the realised error - by more and more as N grows. Halving the
        // claim is therefore refuted only at the first few steps, and asserting it generally
        // would be asserting something false. For s = 2 it is not refuted at any step at all.
        for (int step = 0; step <= 2; step++)
        {
            Approximation halved =
                Approximation.Create(prefix[step].Value, prefix[step].MaxError / 2);

            Assert.False(
                six.AgreesWith(halved),
                Inv($"a half-sized bound survived at s=6, step {step}"));
        }

        Approximation deeper = Approximation.Create(prefix[7].Value, prefix[7].MaxError / 2);
        Assert.True(six.AgreesWith(deeper), "s=6 step 7 unexpectedly refuted a half-sized bound");

        Approximation two = new DirectSumZeta(2).Refinements().First();
        Assert.True(
            ZetaReference.For(2).AgreesWith(Approximation.Create(two.Value, two.MaxError / 2)),
            "s=2 step 0 unexpectedly refuted a half-sized bound");
    }
}
