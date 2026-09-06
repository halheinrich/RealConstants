using static HalHeinrich.Numerics.Tests.Enclosures;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="ZetaReference"/> itself, which is an oracle and therefore has to earn its authority
/// before anything is tested against it.
/// </summary>
/// <remarks>
/// Each digit string is checked from two directions that share nothing with each other:
/// <see cref="DirectSumZeta"/>, which is the definition of zeta(s) with a proven tail bracket and
/// no identity or acceleration anywhere in it, fixes the leading places; and a deep enclosure
/// from a fast provider, with a bound far finer than the sixtieth place, fixes all sixty.
/// </remarks>
public class ZetaReferenceTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public void TheDefinitionAgreesWithTheDigits_WithoutAnyIdentityOrAcceleration(int order)
    {
        ZetaReference reference = ZetaReference.For(order);

        // Two hundred terms of sum 1/k^s, tail bracketed by integrals of x^-s. Slow, shallow,
        // and completely independent of everything else here.
        Approximation fromDefinition = new DirectSumZeta(order).Refinements().Skip(199).First();

        Assert.True(fromDefinition.MaxError.Sign > 0);
        Assert.True(
            reference.AgreesWith(fromDefinition),
            Inv($"the definition and the reference digits disagree at s={order}"));
        Assert.True(
            fromDefinition.Contains(reference.Lower),
            Inv($"the reference's truncated value is outside the definition's enclosure at s={order}"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void TheDigitsAreConfirmedByAComputationFinerThanTheyAre(int order)
    {
        ZetaReference reference = ZetaReference.For(order);

        // Euler-Maclaurin at step 22 has a proven bound around 1e-64, several orders past the
        // reference's last place, so its whole enclosure must sit inside the interval those
        // digits license. One mistyped digit moves that interval by at least 1e-60.
        Approximation deep = new EulerMaclaurinZeta(order).Refinements().Skip(22).First();

        Assert.True(deep.MaxError < TenToTheMinus(63));
        Assert.True(reference.LiesWithin(deep), Inv($"a deep enclosure left the reference at s={order}"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void TheDigitsAreConfirmedASecondTime_ByAProviderSharingNoCodeWithTheFirst(int order)
    {
        ZetaReference reference = ZetaReference.For(order);
        Approximation deep = new CentralBinomialZeta(order).Refinements().Skip(100).First();

        Assert.True(deep.MaxError < TenToTheMinus(63));
        Assert.True(reference.LiesWithin(deep), Inv($"a deep enclosure left the reference at s={order}"));
    }

    [Fact]
    public void TheReferenceRejectsAnOrderItHoldsNoDigitsFor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ZetaReference.For(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => ZetaReference.For(7));
        Assert.Throws<ArgumentOutOfRangeException>(() => ZetaReference.For(1));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public void TheReferenceWouldRejectAWrongDigit(int order)
    {
        ZetaReference reference = ZetaReference.For(order);

        // The predicates have to be capable of failing, or every test built on them is vacuous.
        // A displacement of 1e-55 is far larger than the sixtieth-place window and far smaller
        // than any provider bound tested against it, so it must be refused by all three.
        Approximation shifted = Approximation.Create(
            reference.Lower + TenToTheMinus(55),
            TenToTheMinus(58));

        Assert.False(reference.AgreesWith(shifted));
        Assert.False(reference.LiesWithin(shifted));
        Assert.False(reference.Pins(shifted));
    }

    [Fact]
    public void TheFourReferencesAreDistinct()
    {
        // A copy-paste of one digit string over another would leave two orders agreeing, and
        // every enclosure test for one of them would then be checking the wrong constant.
        Assert.True(ZetaReference.For(2).Lower > ZetaReference.For(3).Lower);
        Assert.True(ZetaReference.For(3).Lower > ZetaReference.For(4).Lower);
        Assert.True(ZetaReference.For(4).Lower > ZetaReference.For(6).Lower);
        Assert.True(ZetaReference.For(6).Lower > 1);
    }
}
