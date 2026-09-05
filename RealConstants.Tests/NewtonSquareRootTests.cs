using System.Globalization;
using System.Numerics;
using static HalHeinrich.Numerics.Tests.Enclosures;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="NewtonSquareRoot"/> against its own contract, at the two radicands the negative
/// control of <c>SPEC-rational-ratio.md</c> section 4 needs.
/// </summary>
/// <remarks>
/// <para>
/// There is no second provider to cross-check against, so the grounding comes from
/// <see cref="SquareRootReference"/> - an independent computation, self-verified here by two
/// integer multiplications. That is an oracle, not a provider pair, and the tests below are
/// named so as not to suggest otherwise.
/// </para>
/// <para>
/// The bound is attacked from three sides: halving the claimed bound must break the enclosure;
/// taking the rational bound on the root from the wrong side must break it too; and both the
/// realised and the planned bound must survive against a reference three hundred decimal places
/// finer than the deepest step tested.
/// </para>
/// </remarks>
public class NewtonSquareRootTests
{
    /// <summary>The largest step whose bound the type is willing to compute.</summary>
    private const int MaxBoundableStep = 30;

    /// <summary>
    /// How many fractional bits the reference keeps. About 308 decimal places, which is finer
    /// than every enclosure tested below except at the steps that say so.
    /// </summary>
    private const int ReferenceBits = 1024;

    // ---------- Construction ----------

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-7)]
    public void Constructor_RejectsARadicandBelowTwo(int radicand) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new NewtonSquareRoot(radicand));

    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    [InlineData(1_000_000)]
    public void Constructor_RejectsAPerfectSquare(int radicand)
    {
        // Refused rather than answered exactly. The root is rational, so the iteration would sit
        // on it from step 0 and every later refinement would repeat it - which is precisely the
        // strictly-improving obligation of IRealConstant.Refinements being broken.
        Assert.Throws<ArgumentOutOfRangeException>(() => new NewtonSquareRoot(radicand));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Radicand_ReportsTheValueConstructedWith(int radicand) =>
        Assert.Equal(new BigInteger(radicand), new NewtonSquareRoot(radicand).Radicand);

    // ---------- ErrorBoundAt ----------

    [Fact]
    public void ErrorBoundAt_RejectsANegativeStep()
    {
        NewtonSquareRoot root = new(2);

        Assert.Throws<ArgumentOutOfRangeException>(() => root.ErrorBoundAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => root.ErrorBoundAt(int.MinValue));
    }

    [Fact]
    public void ErrorBoundAt_RejectsAStepWhoseExponentWouldNotFitAnInt()
    {
        NewtonSquareRoot root = new(2);

        Assert.Throws<ArgumentOutOfRangeException>(() => root.ErrorBoundAt(MaxBoundableStep + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => root.ErrorBoundAt(int.MaxValue));

        // Only the rejecting side of this boundary is testable. Accepting MaxBoundableStep means
        // raising a rational whose terms run to some 34 bits to the power 2^30, and the result
        // needs tens of billions of bits; the guard's value is in the message it gives, not in a
        // limit anything will meet. Memory runs out well before the exponent does, which is what
        // the message says.
    }

    [Fact]
    public void ErrorBoundAt_AtStepZeroIsTheStartingIterateLessTheRationalLowerBound()
    {
        // Written out independently of the implementation. The lower bound on the root is
        // floor(sqrt(c * 2^64)) / 2^32; for two that floor is 6074000999, so the bound at step 0
        // is 2 - 6074000999/2^32 = 2515933593/2^32. For three the floor is 7439101573.
        Assert.Equal(
            new BigRational(2515933593, 4294967296L),
            new NewtonSquareRoot(2).ErrorBoundAt(0));

        Assert.Equal(
            new BigRational(1150833019, 4294967296L),
            new NewtonSquareRoot(3).ErrorBoundAt(0));
    }

    [Fact]
    public void ErrorBoundAt_AtStepOneIsTheSquareOfStepZeroOverTwiceThatLowerBound()
    {
        // The recursion the closed form unrolls, written out as literals at its first step so
        // that the general check below is anchored to something computed by hand.
        Assert.Equal(
            new BigRational(
                BigInteger.Parse("6329921844385889649", CultureInfo.InvariantCulture),
                BigInteger.Parse("52175271293152657408", CultureInfo.InvariantCulture)),
            new NewtonSquareRoot(2).ErrorBoundAt(1));

        Assert.Equal(
            new BigRational(
                BigInteger.Parse("1324416637620654361", CultureInfo.InvariantCulture),
                BigInteger.Parse("63901395935314313216", CultureInfo.InvariantCulture)),
            new NewtonSquareRoot(3).ErrorBoundAt(1));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void ErrorBoundAt_SatisfiesTheRecursionItsClosedFormUnrolls(int radicand)
    {
        NewtonSquareRoot root = new(radicand);
        BigRational twiceLowerBound = 2 * LowerBoundOnRoot(radicand);

        // E_(n+1) = E_n^2 / (2r). The type computes a closed form in the step index instead, so
        // agreeing with the recursion it was derived from is evidence rather than tautology.
        for (int step = 0; step < 10; step++)
        {
            BigRational current = root.ErrorBoundAt(step);

            Assert.Equal(current * current / twiceLowerBound, root.ErrorBoundAt(step + 1));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void ErrorBoundAt_IsStrictlyDecreasing(int radicand)
    {
        NewtonSquareRoot root = new(radicand);

        BigRational previous = root.ErrorBoundAt(0);
        for (int step = 1; step <= 12; step++)
        {
            BigRational current = root.ErrorBoundAt(step);

            Assert.True(current < previous, Inv($"step {step} did not improve on {step - 1}"));
            previous = current;
        }
    }

    [Fact]
    public void ErrorBoundAt_TendsToZero_AndQuadrupleTheAccuracyCostsOneMoreStep()
    {
        IRealConstant two = new NewtonSquareRoot(2);
        IRealConstant three = new NewtonSquareRoot(3);

        // Each step roughly squares the accuracy, so doubling the digits asked for costs one
        // further step rather than doubling the run.
        Assert.Equal(6, two.StepFor(TenToTheMinus(30)));
        Assert.Equal(7, two.StepFor(TenToTheMinus(60)));
        Assert.Equal(8, two.StepFor(TenToTheMinus(100)));

        Assert.Equal(5, three.StepFor(TenToTheMinus(30)));
        Assert.Equal(6, three.StepFor(TenToTheMinus(60)));

        Assert.True(two.ErrorBoundAt(6) <= TenToTheMinus(30));
        Assert.True(two.ErrorBoundAt(5) > TenToTheMinus(30));
    }

    // ---------- Refinements ----------

    [Fact]
    public void Refinements_AreTheNewtonIterates()
    {
        // From x_0 = ceiling(sqrt(c)) = 2 in both cases, by x -> (x + c/x)/2, in exact rationals.
        BigRational[] expectedForTwo =
            [2, Ratio(3, 2), Ratio(17, 12), Ratio(577, 408), Ratio(665857, 470832)];

        BigRational[] expectedForThree =
            [2, Ratio(7, 4), Ratio(97, 56), Ratio(18817, 10864)];

        BigRational[] actualForTwo =
            [.. new NewtonSquareRoot(2).Refinements().Take(expectedForTwo.Length).Select(r => r.Value)];

        BigRational[] actualForThree =
            [.. new NewtonSquareRoot(3).Refinements().Take(expectedForThree.Length).Select(r => r.Value)];

        Assert.Equal(expectedForTwo, actualForTwo);
        Assert.Equal(expectedForThree, actualForThree);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Refinements_DescendTowardTheRootFromAbove(int radicand)
    {
        NewtonSquareRoot root = new(radicand);

        BigRational? previous = null;
        foreach (Approximation refinement in root.Refinements().Take(9))
        {
            // Above the root at every step, which is what the residual x^2 - c being positive
            // says, and what the whole bound rests on.
            Assert.True(refinement.Value * refinement.Value > radicand, "an iterate fell to or below the root");

            if (previous is BigRational last)
            {
                Assert.True(refinement.Value < last, "the iterates must strictly descend");
            }

            previous = refinement.Value;
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Refinements_AreStrictlyImproving(int radicand)
    {
        NewtonSquareRoot root = new(radicand);

        BigRational? previous = null;
        foreach (Approximation refinement in root.Refinements().Take(9))
        {
            if (previous is BigRational last)
            {
                Assert.True(refinement.MaxError < last, "MaxError must strictly decrease");
            }

            previous = refinement.MaxError;
        }
    }

    [Fact]
    public void Refinements_CarryTheRealisedBound_WhichIsSharperThanTheClosedForm()
    {
        // The realised bound at step 0 is (x_0^2 - c)/(x_0 + r), which for two is
        // 2/(2 + 6074000999/2^32) = 2^33/14663935591, and for three is 1/(2 + 7439101573/2^32).
        Assert.Equal(
            new BigRational(8589934592L, 14663935591L),
            new NewtonSquareRoot(2).Refinements().First().MaxError);

        Assert.Equal(
            new BigRational(4294967296L, 16029036165L),
            new NewtonSquareRoot(3).Refinements().First().MaxError);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Refinements_NeverClaimMoreThanTheErrorFunctionPromised(int radicand)
    {
        NewtonSquareRoot root = new(radicand);

        // The realised bound is at or below the planned one at every step, so a step chosen from
        // ErrorBoundAt delivers at least what it promised. The two are separate derivations -
        // one reads the iterate, the other never sees it - so this is a real comparison.
        int step = 0;
        foreach (Approximation refinement in root.Refinements().Take(11))
        {
            Assert.True(
                refinement.MaxError <= root.ErrorBoundAt(step),
                Inv($"step {step} realised {refinement.MaxError}, above its promised bound"));
            step++;
        }

        Assert.Equal(11, step);
    }

    [Fact]
    public void Refinements_AreIndependentBetweenEnumerations()
    {
        NewtonSquareRoot root = new(2);

        Approximation[] first = [.. root.Refinements().Take(4)];
        Approximation[] second = [.. root.Refinements().Take(4)];

        Assert.Equal(first, second);
    }

    // ---------- The reference itself ----------

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void TheReferenceIsSelfVerifying(int radicand)
    {
        SquareRootReference reference = SquareRootReference.For(radicand, ReferenceBits);

        // The oracle's whole claim, settled by two integer multiplications: the scaled root is
        // the floor of the square root of the scaled radicand. Nothing here trusts IntegerMath;
        // this is the check that would catch it being wrong.
        BigInteger claimed = reference.ScaledRoot;

        Assert.True(claimed * claimed <= reference.ScaledRadicand, "the reference root squares above the radicand");
        Assert.True(
            (claimed + BigInteger.One) * (claimed + BigInteger.One) > reference.ScaledRadicand,
            "the reference root is not the largest one that fits");
    }

    // ---------- The bound, tested by trying to violate it ----------

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Refinements_PinTheRootWhileTheirBoundIsWiderThanTheReference(int radicand)
    {
        NewtonSquareRoot root = new(radicand);
        SquareRootReference reference = SquareRootReference.For(radicand, ReferenceBits);

        // Steps 0 to 8 all claim a bound wider than the reference's own window of about 5.6e-309,
        // so the strong containment is the right assertion. Past that the enclosure is the finer
        // of the two and the direction reverses; see the next test.
        int step = 0;
        foreach (Approximation refinement in root.Refinements().Take(9))
        {
            Assert.True(
                reference.Pins(refinement),
                Inv($"step {step} claims a bound of {refinement.MaxError} that excludes the root"));
            step++;
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Refinements_LieWithinTheReferenceOnceFinerThanIt(int radicand)
    {
        NewtonSquareRoot root = new(radicand);
        SquareRootReference reference = SquareRootReference.For(radicand, ReferenceBits);

        // Step 9's realised bound is past the reference's last place in both cases, so the whole
        // enclosure must sit inside the reference interval. This is the converse containment,
        // and it is what would catch an enclosure that had drifted off the root entirely while
        // still being wide enough to overlap.
        Approximation deep = root.Refinements().Skip(9).First();

        Assert.True(deep.MaxError < reference.Upper - reference.Lower);
        Assert.True(
            reference.LiesWithin(deep),
            Inv($"a nine-step enclosure of half-width {deep.MaxError} fell outside the reference"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void ThePlannedBoundAlsoEnclosesTheRootAtEveryStepTested(int radicand)
    {
        NewtonSquareRoot root = new(radicand);
        SquareRootReference reference = SquareRootReference.For(radicand, ReferenceBits);

        // ErrorBoundAt is a bound in its own right, not merely a budget for the realised one, so
        // it is tested against the reference on its own terms.
        int step = 0;
        foreach (Approximation refinement in root.Refinements().Take(9))
        {
            Approximation planned = Approximation.Create(refinement.Value, root.ErrorBoundAt(step));

            Assert.True(
                reference.Pins(planned),
                Inv($"the planned bound at step {step} excludes the root"));
            step++;
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Refinements_WouldStopEnclosingTheRoot_IfTheClaimedBoundWereHalved(int radicand)
    {
        NewtonSquareRoot root = new(radicand);
        SquareRootReference reference = SquareRootReference.For(radicand, ReferenceBits);

        // The realised bound is close to tight - it overstates the true error only by the factor
        // (x + sqrt(c))/(x + r), which is within 2^-32 of one - so halving it is refuted at every
        // step, not just where the bound is coarse. That is what makes the enclosure tests above
        // capable of failing.
        int step = 0;
        foreach (Approximation refinement in root.Refinements().Take(7))
        {
            Approximation halved = Approximation.Create(refinement.Value, refinement.MaxError / 2);

            Assert.True(reference.AgreesWith(refinement));
            Assert.False(
                reference.AgreesWith(halved),
                Inv($"a half-sized bound survived at step {step}"));
            step++;
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void TheBoundWouldBeViolated_IfTheRationalBoundOnTheRootCameFromTheWrongSide(int radicand)
    {
        NewtonSquareRoot root = new(radicand);
        SquareRootReference reference = SquareRootReference.For(radicand, ReferenceBits);

        // The one substitution the whole derivation turns on: sqrt(c) is replaced by something at
        // or below it, so that dividing by the replacement can only enlarge the quotient. Take
        // the replacement from above instead - the very next integer, one part in 2^32 away - and
        // the realised bound falls under the true error and stops enclosing the root, at every
        // step and by a margin the reference can see.
        BigRational fromAbove = LowerBoundOnRoot(radicand) + new BigRational(BigInteger.One, BigInteger.One << 32);

        Assert.True(fromAbove * fromAbove > radicand, "the trap's replacement must actually exceed the root");

        int step = 0;
        foreach (Approximation refinement in root.Refinements().Take(3))
        {
            BigRational residual = (refinement.Value * refinement.Value) - radicand;
            Approximation wrong = Approximation.Create(refinement.Value, residual / (refinement.Value + fromAbove));

            Assert.True(reference.AgreesWith(refinement));
            Assert.False(
                reference.AgreesWith(wrong),
                Inv($"a bound built from an upper bound on the root survived at step {step}"));
            step++;
        }
    }

    // ---------- Fitness for the negative control's division ----------

    [Fact]
    public void TheTwoRootsDivide_WithTheQuotientEnclosingTheirRatio()
    {
        // Not the negative control, which is a trend matrix and belongs to the consumer. This is
        // the one property of these providers the control depends on: that a root-of-three
        // enclosure excludes zero, so Approximation's division accepts it, and that the quotient
        // it propagates still encloses the true ratio.
        Approximation two = new NewtonSquareRoot(2).Refinements().Skip(4).First();
        Approximation three = new NewtonSquareRoot(3).Refinements().Skip(4).First();

        Assert.True(three.ExcludesZero);

        Approximation quotient = two / three;

        SquareRootReference referenceTwo = SquareRootReference.For(2, ReferenceBits);
        SquareRootReference referenceThree = SquareRootReference.For(3, ReferenceBits);

        // The true ratio lies between the reference quotients taken at opposite ends.
        Assert.True(quotient.Lower <= referenceTwo.Lower / referenceThree.Upper);
        Assert.True(referenceTwo.Upper / referenceThree.Lower <= quotient.Upper);
    }

    /// <summary>
    /// Rebuilds the rational lower bound on the root that the provider uses, for the tests that
    /// need to name it.
    /// </summary>
    /// <param name="radicand">The radicand.</param>
    /// <returns><c>floor(sqrt(radicand * 2^64)) / 2^32</c>.</returns>
    private static BigRational LowerBoundOnRoot(int radicand) =>
        new(
            IntegerMath.Sqrt(new BigInteger(radicand) << 64, IntegerSqrtRounding.Floor),
            BigInteger.One << 32);
}
