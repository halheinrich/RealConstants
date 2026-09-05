using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The square root of an integer as an external reference: a rational interval of width
/// <c>2^-fractionBits</c> around it, computed by <c>IntegerMath.Sqrt</c> on a scaled integer.
/// </summary>
/// <remarks>
/// <para>
/// This is the oracle the square-root bounds are tested against, and it plays the part
/// <c>PiReference</c> plays for the pi pair - with one difference that is worth naming rather
/// than glossing. Pi's reference is a hardcoded digit string, checked against a computation
/// because nothing else can check it. This one is computed, and it is <b>self-verifying</b>:
/// <see cref="ScaledRoot"/> is claimed to be <c>floor(sqrt(ScaledRadicand))</c>, and that claim
/// is settled by two integer multiplications, which <c>NewtonSquareRootTests</c> performs. So
/// the oracle rests on an assertion a reader can check by hand rather than on trust in
/// <c>IntegerMath</c>.
/// </para>
/// <para>
/// It is <b>not</b> a second provider, and agreement with it is not a provider cross-check.
/// <c>SPEC-rational-ratio.md</c> section 4 names cross-check pairs for pi and for zeta(3) and
/// names none for square roots. What this gives is grounding: evidence that the enclosures
/// contain the number they claim to, at a precision the provider under test can be pushed past.
/// </para>
/// <para>
/// Truncated rather than rounded, for the same reason pi's reference is: the floor puts the root
/// in <c>[Lower, Lower + 2^-fractionBits]</c> with no case analysis, where a nearest rounding
/// would need the direction argued. Both endpoints are used as closed bounds, which is sound
/// under truncation whichever way the omitted bits fall.
/// </para>
/// </remarks>
internal sealed class SquareRootReference
{
    private SquareRootReference(BigInteger radicand, int fractionBits)
    {
        Radicand = radicand;
        FractionBits = fractionBits;

        ScaledRadicand = radicand << (2 * fractionBits);
        ScaledRoot = IntegerMath.Sqrt(ScaledRadicand, IntegerSqrtRounding.Floor);

        BigInteger scale = BigInteger.One << fractionBits;
        Lower = new BigRational(ScaledRoot, scale);
        Upper = Lower + new BigRational(BigInteger.One, scale);
    }

    /// <summary>Gets the integer whose square root this brackets.</summary>
    public BigInteger Radicand { get; }

    /// <summary>Gets how many fractional bits the truncation keeps.</summary>
    public int FractionBits { get; }

    /// <summary>Gets the radicand scaled by <c>2^(2*FractionBits)</c>, whose floor root is taken.</summary>
    public BigInteger ScaledRadicand { get; }

    /// <summary>Gets the integer claimed to be <c>floor(sqrt(ScaledRadicand))</c>.</summary>
    public BigInteger ScaledRoot { get; }

    /// <summary>Gets the truncated root. The true root is at or above this.</summary>
    public BigRational Lower { get; }

    /// <summary>Gets one unit in the last place above <see cref="Lower"/>. The true root is at or below this.</summary>
    public BigRational Upper { get; }

    /// <summary>Builds a reference for the square root of an integer.</summary>
    /// <param name="radicand">The integer whose root is wanted. Must be non-negative.</param>
    /// <param name="fractionBits">How many fractional bits to keep. Must be positive.</param>
    /// <returns>The interval the truncation licenses.</returns>
    public static SquareRootReference For(BigInteger radicand, int fractionBits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radicand);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fractionBits);

        return new SquareRootReference(radicand, fractionBits);
    }

    /// <summary>
    /// Determines whether an enclosure certainly contains the root, by containing the whole
    /// reference interval.
    /// </summary>
    /// <param name="enclosure">The enclosure to test.</param>
    /// <returns><see langword="true"/> if every point the reference admits lies inside it.</returns>
    /// <remarks>
    /// The strong form, and the one a bound test wants. It can only be asked of an enclosure
    /// wider than the reference; a finer one is tested with <see cref="LiesWithin"/> instead.
    /// </remarks>
    public bool Pins(Approximation enclosure) =>
        enclosure.Lower <= Lower && Upper <= enclosure.Upper;

    /// <summary>Determines whether an enclosure is finer than the reference and sits inside it.</summary>
    /// <param name="enclosure">The enclosure to test.</param>
    /// <returns><see langword="true"/> if the enclosure is contained in the reference interval.</returns>
    public bool LiesWithin(Approximation enclosure) =>
        Lower <= enclosure.Lower && enclosure.Upper <= Upper;

    /// <summary>Determines whether an enclosure is consistent with the reference, at any width.</summary>
    /// <param name="enclosure">The enclosure to test.</param>
    /// <returns><see langword="true"/> if the enclosure and the reference interval overlap.</returns>
    /// <remarks>
    /// The weak form, and the only one that applies at every width. A false here is a
    /// refutation: the enclosure and the reference cannot both hold.
    /// </remarks>
    public bool AgreesWith(Approximation enclosure) =>
        enclosure.Lower <= Upper && Lower <= enclosure.Upper;
}
