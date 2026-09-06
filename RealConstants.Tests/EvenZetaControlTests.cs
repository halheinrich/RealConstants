using static HalHeinrich.Numerics.Tests.Enclosures;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The premise the positive controls of <c>SPEC-rational-ratio.md</c> section 4 rest on:
/// <c>pi^2/zeta(2) = 6</c>, <c>pi^4/zeta(4) = 90</c> and <c>pi^6/zeta(6) = 945</c>, computed
/// through the same enclose-power-divide path the pipeline will use.
/// </summary>
/// <remarks>
/// <para>
/// <c>Zeta</c> will run these as controls, and a control that fails tells its owner the pipeline
/// is broken. If the premise itself were wrong - a mistyped target, a provider that does not
/// converge to what its name says - that session would spend its time auditing correct wiring.
/// So the premise is established here, in the repository that owns the providers.
/// </para>
/// <para>
/// <b>This is not a tautology, and the reason it is not is the reason these providers exist.</b>
/// zeta(2n) is a rational multiple of pi^(2n). A provider computing zeta(2) as pi^2/6 would make
/// this test pass by construction, on any value of pi whatever, and the control would be
/// checking nothing. <see cref="EulerMaclaurinZeta"/> and <see cref="CentralBinomialZeta"/> reach
/// zeta(s) from reciprocal powers, binomial coefficients and Bernoulli numbers, and never form
/// pi at all. The agreement below is therefore evidence about both sides.
/// </para>
/// <para>
/// The division is the one from section 2, propagated by <see cref="Approximation"/>, and the
/// power is <c>Pow</c> rather than repeated multiplication - which matters, because
/// <c>a * a</c> treats its operands as independent and would widen the enclosure at every
/// squaring.
/// </para>
/// </remarks>
public class EvenZetaControlTests
{
    /// <summary>How deep the pi provider is taken. Its bound here is around 1.7e-66.</summary>
    private const int PiStep = 45;

    /// <summary>How deep the zeta provider is taken. Its bound here is around 1e-64.</summary>
    private const int ZetaStep = 22;

    private static Approximation PiPower(int exponent) =>
        new MachinPi().Refinements().Skip(PiStep).First().Pow(exponent);

    [Theory]
    [InlineData(2, 6)]
    [InlineData(4, 90)]
    [InlineData(6, 945)]
    public void ThePositiveControlsHold_AgainstAZetaProviderThatNeverFormsPi(int order, int target)
    {
        Approximation zeta = new EulerMaclaurinZeta(order).Refinements().Skip(ZetaStep).First();

        Assert.True(zeta.ExcludesZero, "the divisor's enclosure must exclude zero");

        Approximation ratio = PiPower(order) / zeta;

        Assert.True(
            ratio.Contains(target),
            Inv($"pi^{order}/zeta({order}) = {ratio.Value} +- {ratio.MaxError} does not contain {target}"));
    }

    [Theory]
    [InlineData(2, 6)]
    [InlineData(4, 90)]
    public void ThePositiveControlsHold_AgainstTheOtherZetaProviderToo(int order, int target)
    {
        // The same premise through the other method, since the two share no code. If one of them
        // converged to something that is not zeta(s), only one of these two would fail.
        Approximation zeta = new CentralBinomialZeta(order).Refinements().Skip(100).First();
        Approximation ratio = PiPower(order) / zeta;

        Assert.True(ratio.Contains(target), Inv($"pi^{order}/zeta({order}) does not contain {target}"));
    }

    [Theory]
    [InlineData(2, 6)]
    [InlineData(4, 90)]
    [InlineData(6, 945)]
    public void TheControlsAreSharpEnoughToRefuteANeighbouringTarget(int order, int target)
    {
        // A control that merely contained its target would be satisfied by an enclosure a
        // thousand wide. What makes it a control is that it excludes everything else: the
        // propagated half-width here is under 1e-55, so the nearest integers are refuted by
        // fifty-odd orders of magnitude, and so is a target wrong in its fortieth decimal.
        Approximation zeta = new EulerMaclaurinZeta(order).Refinements().Skip(ZetaStep).First();
        Approximation ratio = PiPower(order) / zeta;

        Assert.True(ratio.MaxError < TenToTheMinus(55), Inv($"the propagated bound was {ratio.MaxError}"));

        Assert.False(ratio.Contains(target - 1));
        Assert.False(ratio.Contains(target + 1));
        Assert.False(ratio.Contains(target + TenToTheMinus(40)));
        Assert.False(ratio.Contains(target - TenToTheMinus(40)));
    }

    [Fact]
    public void TheControlsWouldFail_IfTheTargetsWereSwappedBetweenOrders()
    {
        // The assertion that makes the three above capable of failing together. 6, 90 and 945 are
        // not interchangeable, and a test that only ever checked "some integer is in there" would
        // not notice if they were.
        (int Order, int Wrong)[] swaps = [(2, 90), (4, 945), (6, 6)];

        foreach ((int order, int wrong) in swaps)
        {
            Approximation zeta = new EulerMaclaurinZeta(order).Refinements().Skip(ZetaStep).First();
            Approximation ratio = PiPower(order) / zeta;

            Assert.False(
                ratio.Contains(wrong),
                Inv($"pi^{order}/zeta({order}) contained the wrong target {wrong}"));
        }
    }

    [Fact]
    public void PowIsNotRepeatedMultiplication_WhichIsWhyTheControlsAreThisSharp()
    {
        // Section 2 names this as load-bearing at step 1 of the method, and the controls are the
        // first place it bites. Squaring by multiplication treats the two operands as independent
        // unknowns and widens the result; Pow re-centres from the endpoints and does not.
        Approximation pi = new MachinPi().Refinements().Skip(PiStep).First();

        Approximation byPow = pi.Pow(6);
        Approximation byMultiplication = pi * pi * pi * pi * pi * pi;

        Assert.True(byPow.MaxError < byMultiplication.MaxError);
        Assert.True(Contain(byMultiplication, byPow));
    }
}
