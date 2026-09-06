using System.Numerics;
using HalHeinrich.Numerics.Experiments;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="Presentation"/>, and above all the renderer that decides which digits of an
/// enclosure a reader is allowed to see.
/// </summary>
/// <remarks>
/// <c>../VISION.md</c> § Guiding principles asks that an unearned digit be unrepresentable rather
/// than discouraged. The walk's value column is where that is either honoured or not, so its
/// renderer is tested against the cases a plausible implementation gets wrong - a carry between
/// the two bounds, and bounds whose integer parts differ in width.
/// </remarks>
public class PresentationTests
{
    /// <summary>Builds an enclosure from two integers over a power of ten.</summary>
    private static Approximation At(long value, long error, int scale)
    {
        BigInteger denominator = BigInteger.Pow(10, scale);
        return Approximation.Create(
            new BigRational(value, denominator),
            new BigRational(error, denominator));
    }

    [Fact]
    public void OnlyTheDigitsBothBoundsAgreeOnAreShown()
    {
        // [1.2345678909999990, 1.2345678910000010] agrees to eight decimal places and no further:
        // the ninth is 0 at one end and 1 at the other.
        Approximation enclosure = At(1_234_567_891_000_000, 1_000_000, 15);

        Assert.Equal("1.23456789", Presentation.Earned(enclosure, 20));
    }

    [Fact]
    public void ACarryBetweenTheBoundsEarnsNothingAfterThePoint()
    {
        // [1.20, 1.30]. The first decimal could be 2 or 3, so it is not shown - and a digit count
        // derived from the bound would have claimed one place here.
        Approximation enclosure = At(125, 5, 2);

        Assert.Equal("1.", Presentation.Earned(enclosure, 20));
    }

    [Fact]
    public void BoundsThatDisagreeInTheirFirstDigitEarnNothingAtAll()
    {
        // [0.8, 1.2]: not even the units digit is pinned.
        Assert.Equal("?", Presentation.Earned(At(10, 2, 1), 20));
    }

    [Fact]
    public void BoundsWhoseIntegerPartsDifferInWidthEarnNothingAtAll()
    {
        // [9.9, 10.1]. This is the case a naive renderer gets wrong: it compares fractional
        // parts, or slices a digit count off the bound, and reports a shared digit where the two
        // renderings do not even line up.
        Assert.Equal("?", Presentation.Earned(At(100, 1, 1), 20));
    }

    [Fact]
    public void AnEnclosureStraddlingZeroEarnsNothingAtAll()
    {
        Assert.Equal("?", Presentation.Earned(At(0, 1, 0), 20));
    }

    [Fact]
    public void AnExactEnclosurePinsEveryDigitItHas()
    {
        // Nothing is unknown, so the cap is what stops it rather than the bounds parting.
        Approximation exact = Approximation.Exact(new BigRational(5, 4));

        Assert.Equal("1.2500000000...", Presentation.Earned(exact, 10));
    }

    [Fact]
    public void APrefixReachingTheCapSaysThereIsMore()
    {
        // A tight enclosure whose agreement outruns the places asked for. The ellipsis is the
        // difference between "this is all that is known" and "this is all that was printed".
        //
        // The centre is deliberately away from a decimal boundary. 1.5 with the same width would
        // straddle a carry and earn "1." however tight it was, which is correct behaviour and
        // makes a poor fixture for this property - it was the first one written here, and it
        // failed for that reason rather than for the one the test is about.
        Approximation tight = At(1_234_567_891_234, 1, 12);

        string rendered = Presentation.Earned(tight, 4);

        Assert.Equal("1.2345...", rendered);
        Assert.EndsWith("...", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void TheColumnGrowsAsAnEnclosureTightens()
    {
        // The property that makes the column worth reading: it is a picture of convergence, not a
        // fixed-width field with noise in it. Checked against a real provider rather than
        // constructed enclosures, so it is the walk's actual behaviour being asserted.
        int previous = 0;

        foreach (Approximation refinement in new NewtonSquareRoot(2).Refinements().Take(6).Skip(1))
        {
            int shown = Presentation.Earned(refinement, 60).TrimEnd('.').Length;

            Assert.True(shown >= previous, "the earned-digit column must not shrink as the walk converges");
            previous = shown;
        }

        Assert.True(previous > 20, "six Newton steps should earn more than twenty characters");
    }

    [Fact]
    public void EveryShownDigitIsOneBothBoundsAgreeOn()
    {
        // The assertion the principle actually reduces to, over a provider that starts coarse and
        // ends fine. Whatever the renderer prints must be a prefix of both bounds' own rendering.
        foreach (Approximation refinement in new CentralBinomialZeta(3).Refinements().Take(12))
        {
            string shown = Presentation.Earned(refinement, 40);
            if (shown == "?")
            {
                continue;
            }

            string bare = shown.EndsWith("...", StringComparison.Ordinal) ? shown[..^3] : shown;

            Assert.StartsWith(bare, Presentation.ToDecimal(refinement.Lower, 40), StringComparison.Ordinal);
            Assert.StartsWith(bare, Presentation.ToDecimal(refinement.Upper, 40), StringComparison.Ordinal);
        }
    }
}
