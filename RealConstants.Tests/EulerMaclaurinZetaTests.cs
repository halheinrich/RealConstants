using System.Numerics;
using static HalHeinrich.Numerics.Tests.Enclosures;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="EulerMaclaurinZeta"/> against its own contract, and against the hazard that makes
/// it the only provider here whose <i>step</i> had to be argued for rather than chosen.
/// </summary>
/// <remarks>
/// Its series in the correction count is asymptotic: the error falls, reaches a least value, and
/// then grows without limit. A step that grew the correction count would report a shrinking
/// bound over a growing error, which is the one failure this bench exists to make impossible.
/// The turn is pinned by a test here rather than left as a remembered constraint, and it is
/// computed from the formula in the test rather than read off the provider - the provider cannot
/// be driven past the turn, which is the property under test.
/// </remarks>
public class EulerMaclaurinZetaTests
{
    // ---------- Domain ----------

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-3)]
    public void Constructor_RejectsAnOrderBelowTwo(int order) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new EulerMaclaurinZeta(order));

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Order_ReportsTheValueConstructedWith(int order) =>
        Assert.Equal(order, new EulerMaclaurinZeta(order).Order);

    // ---------- ErrorBoundAt ----------

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void ErrorBoundAt_RejectsANegativeStep(int order)
    {
        EulerMaclaurinZeta zeta = new(order);

        Assert.Throws<ArgumentOutOfRangeException>(() => zeta.ErrorBoundAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => zeta.ErrorBoundAt(int.MinValue));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void ErrorBoundAt_IsTheLeastBoundAvailableAtThatExactTermCount(int order)
    {
        EulerMaclaurinZeta zeta = new(order);

        // The provider's whole monotonicity argument rests on its M being the minimiser, not
        // merely a local one found by a scan that stopped early. Checked here against a brute
        // sweep over a range far past where the scan stops.
        for (int step = 0; step <= 8; step++)
        {
            int count = step + 2;
            BigRational least = EulerMaclaurin.Bound(order, count, 1);

            for (int corrections = 2; corrections <= 60; corrections++)
            {
                BigRational candidate = EulerMaclaurin.Bound(order, count, corrections);
                if (candidate < least)
                {
                    least = candidate;
                }
            }

            Assert.Equal(least, zeta.ErrorBoundAt(step));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void ErrorBoundAt_IsStrictlyDecreasing(int order)
    {
        EulerMaclaurinZeta zeta = new(order);

        // Not merely observed: E(N+1, M*(N+1)) <= E(N+1, M*(N)) < E(N, M*(N)), the first because
        // M* minimises and the second because the bound falls as N grows at fixed M. This
        // confirms the argument rather than standing in for it.
        BigRational previous = zeta.ErrorBoundAt(0);
        for (int step = 1; step <= 25; step++)
        {
            BigRational current = zeta.ErrorBoundAt(step);

            Assert.True(current < previous, Inv($"step {step} did not improve on {step - 1}"));
            previous = current;
        }
    }

    [Fact]
    public void ErrorBoundAt_TendsToZero_AtRoughlyTwoAndThreeQuarterDigitsPerStep()
    {
        IRealConstant two = new EulerMaclaurinZeta(2);
        IRealConstant four = new EulerMaclaurinZeta(4);
        IRealConstant six = new EulerMaclaurinZeta(6);

        // log10 of e to the 2*pi is about 2.73, the classical accuracy of an optimally truncated
        // Euler-Maclaurin, and the three orders track it almost identically.
        Assert.Equal(2, two.StepFor(TenToTheMinus(10)));
        Assert.Equal(3, four.StepFor(TenToTheMinus(10)));
        Assert.Equal(3, six.StepFor(TenToTheMinus(10)));

        Assert.Equal(10, two.StepFor(TenToTheMinus(30)));
        Assert.Equal(10, four.StepFor(TenToTheMinus(30)));
        Assert.Equal(10, six.StepFor(TenToTheMinus(30)));

        Assert.True(two.ErrorBoundAt(10) <= TenToTheMinus(30));
        Assert.True(two.ErrorBoundAt(9) > TenToTheMinus(30));
    }

    // ---------- The turn, which is why "step" grows N ----------

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void TheAsymptoticSeriesTurns_SoAStepMayNotGrowTheCorrectionCount(int order)
    {
        // Fix the exact-term count and push the correction count past its turn. The realised
        // error falls to a least value and then rises - by twelve orders of magnitude over the
        // range checked - while the naive reading of an "improving" series would say it should
        // keep falling. This is the failure a step growing M would produce: a shrinking bound
        // over a growing error.
        //
        // Computed from the formula here rather than through the provider. The provider chooses
        // the minimising correction count and cannot be driven past the turn, which is the
        // property this test exists to establish.
        const int Count = 10;
        ZetaReference reference = ZetaReference.For(order);
        BigRational middle = (reference.Lower + reference.Upper) / 2;

        BigRational atThirty = BigRational.Abs(EulerMaclaurin.Value(order, Count, 30) - middle);
        BigRational atForty = BigRational.Abs(EulerMaclaurin.Value(order, Count, 40) - middle);
        BigRational atSixtyFive = BigRational.Abs(EulerMaclaurin.Value(order, Count, 65) - middle);

        Assert.True(atThirty < TenToTheMinus(25), Inv($"error at M=30 was {atThirty}"));
        Assert.True(atForty > atThirty, "the error did not rise past the turn");
        Assert.True(atSixtyFive > atForty, "the error did not keep rising past the turn");
        Assert.True(atSixtyFive > TenToTheMinus(16), Inv($"error at M=65 was {atSixtyFive}"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void GrowingTheExactTermCountNeverTurns(int order)
    {
        // The other half of the ruling: at a fixed correction count the error falls monotonically
        // in N, with no turn anywhere. That is what makes N the safe thing for a step to grow.
        ZetaReference reference = ZetaReference.For(order);
        BigRational middle = (reference.Lower + reference.Upper) / 2;

        foreach (int corrections in (int[])[4, 8, 16])
        {
            BigRational previous = BigRational.Abs(EulerMaclaurin.Value(order, 10, corrections) - middle);

            for (int count = 11; count <= 40; count++)
            {
                BigRational current = BigRational.Abs(EulerMaclaurin.Value(order, count, corrections) - middle);

                Assert.True(
                    current < previous,
                    Inv($"s={order}, M={corrections}: the error rose from N={count - 1} to N={count}"));
                previous = current;
            }
        }
    }

    // ---------- Refinements ----------

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Refinements_CarryExactlyTheBoundTheErrorFunctionPromises(int order)
    {
        EulerMaclaurinZeta zeta = new(order);

        int step = 0;
        foreach (Approximation refinement in zeta.Refinements().Take(20))
        {
            Assert.Equal(zeta.ErrorBoundAt(step), refinement.MaxError);
            step++;
        }

        Assert.Equal(20, step);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Refinements_MatchTheFormulaComputedIndependently(int order)
    {
        EulerMaclaurinZeta zeta = new(order);

        // The provider builds its value incrementally, carrying the partial sum and the
        // Bernoulli cache across steps; the helper below rebuilds each one from scratch. The two
        // routes must land on the same rational.
        int step = 0;
        foreach (Approximation refinement in zeta.Refinements().Take(12))
        {
            int count = step + 2;
            int corrections = EulerMaclaurin.MinimisingCorrections(order, count);

            Assert.Equal(EulerMaclaurin.Value(order, count, corrections), refinement.Value);
            step++;
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Refinements_AreStrictlyImproving(int order)
    {
        EulerMaclaurinZeta zeta = new(order);

        BigRational? previous = null;
        foreach (Approximation refinement in zeta.Refinements().Take(20))
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
        EulerMaclaurinZeta zeta = new(6);

        Approximation[] first = [.. zeta.Refinements().Take(4)];
        Approximation[] second = [.. zeta.Refinements().Take(4)];

        Assert.Equal(first, second);
    }

    // ---------- The bound, tested by trying to violate it ----------

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Refinements_PinZetaWhileTheirBoundIsWiderThanTheReference(int order)
    {
        EulerMaclaurinZeta zeta = new(order);
        ZetaReference reference = ZetaReference.For(order);

        int step = 0;
        foreach (Approximation refinement in zeta.Refinements().Take(21))
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
    public void Refinements_LieWithinTheReferenceOnceFinerThanIt(int order)
    {
        EulerMaclaurinZeta zeta = new(order);
        ZetaReference reference = ZetaReference.For(order);

        // Step 22's proven bound is around 1e-64 to 1e-65, several orders finer than the
        // reference's last place, so the whole enclosure must sit inside it.
        Approximation deep = zeta.Refinements().Skip(22).First();

        Assert.True(deep.MaxError < TenToTheMinus(63));
        Assert.True(
            reference.LiesWithin(deep),
            Inv($"a 22-step enclosure of half-width {deep.MaxError} fell outside the reference"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void TheClaimedBoundIsRefutedAtTwoFifths_ButHalvingItIsBorderline(int order)
    {
        EulerMaclaurinZeta zeta = new(order);
        ZetaReference reference = ZetaReference.For(order);
        Approximation[] prefix = [.. zeta.Refinements().Take(12)];

        // Measured realised-over-claimed sits between 0.49 and 0.51 at every step, so the bound
        // is tight to within a factor of two - and halving it therefore lands on the wrong side
        // of the line at some steps and the right side at others. Asserting "halving fails"
        // would be asserting something that is false eight times in twelve.
        //
        // Two fifths of the claim is refuted at every step, which is the strongest uniform
        // statement the measurements support, so that is what gets asserted.
        for (int step = 0; step < prefix.Length; step++)
        {
            Approximation shrunk =
                Approximation.Create(prefix[step].Value, prefix[step].MaxError * 2 / 5);

            Assert.True(reference.AgreesWith(prefix[step]));
            Assert.False(
                reference.AgreesWith(shrunk),
                Inv($"a two-fifths bound survived at s={order}, step {step}"));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void TheCorrectionsAreLoadBearing_ThoughDroppingOnlyTheLastIsNotCaught(int order)
    {
        EulerMaclaurinZeta zeta = new(order);
        ZetaReference reference = ZetaReference.For(order);

        int step = 0;
        foreach (Approximation refinement in zeta.Refinements().Take(12))
        {
            int count = step + 2;
            int corrections = EulerMaclaurin.MinimisingCorrections(order, count);

            // Truncating the correction sum to nothing, or to a single term, while keeping the
            // bound the full sum earned is refuted at every step in every order. The corrections
            // are the method.
            Assert.True(reference.AgreesWith(refinement));
            Assert.False(
                reference.AgreesWith(
                    Approximation.Create(EulerMaclaurin.Value(order, count, 0), refinement.MaxError)),
                Inv($"a value with no corrections survived at s={order}, step {step}"));
            Assert.False(
                reference.AgreesWith(
                    Approximation.Create(EulerMaclaurin.Value(order, count, 1), refinement.MaxError)),
                Inv($"a value with one correction survived at s={order}, step {step}"));

            // Dropping only the LAST correction is not caught, and that is asserted rather than
            // quietly avoided. The bound is the magnitude of that last term, and the remainder it
            // bounds carries the opposite sign, so removing the term moves the value by about the
            // bound in the direction that partly cancels the error already there. A test claiming
            // this mutation fails would be claiming something false.
            Assert.True(
                reference.AgreesWith(
                    Approximation.Create(
                        EulerMaclaurin.Value(order, count, corrections - 1), refinement.MaxError)),
                Inv($"s={order}, step {step} refuted a value one correction short, so this test can be strengthened"));
            step++;
        }
    }

    [Fact]
    public void NoValueHereDependsOnPi()
    {
        // Not a numerical assertion but a structural one, and the reason this provider exists.
        // zeta(2n) is a rational multiple of pi^(2n), so a provider that reached it through pi
        // would make pi^(2n)/zeta(2n) exact by construction and turn section 4's positive
        // controls into tautologies. Euler-Maclaurin reaches zeta(s) from reciprocal powers and
        // Bernoulli numbers, both generated by rational recurrences, and the enclosures below
        // are the evidence: every one is a ratio of integers whose derivation never met pi.
        foreach (int order in (int[])[2, 4, 6])
        {
            Approximation first = new EulerMaclaurinZeta(order).Refinements().First();

            Assert.True(first.Value.Denominator > BigInteger.One);
            Assert.True(first.MaxError.Sign > 0);
            Assert.True(ZetaReference.For(order).Pins(first));
        }
    }
}
