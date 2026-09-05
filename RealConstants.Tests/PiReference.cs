using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// Pi as an external reference: the published decimal expansion, truncated at sixty places, held
/// as the exact rational interval that truncation licenses.
/// </summary>
/// <remarks>
/// <para>
/// This is a hardcoded digit string, which the design deliberately prefers <i>not</i> to lean on
/// where a cross-check between two independent providers will do. It is here because the two
/// jobs are different. A cross-check establishes that the providers agree; it cannot establish
/// that they agree on pi rather than on some other number, and it cannot check a bound at a
/// precision the coarser provider never reaches. Grounding needs an outside value, and a bound
/// is tested by trying to violate it against one.
/// </para>
/// <para>
/// The digits are not taken on trust. <c>MachinPiTests</c> asserts that this interval brackets
/// a Machin enclosure whose own proven bound is far narrower than the sixtieth place, so a
/// mistyped digit anywhere in the string fails a test rather than silently weakening every
/// oracle built on it.
/// </para>
/// <para>
/// Truncated rather than rounded, on purpose: truncation puts the true value in
/// <c>[Lower, Lower + 10^-60)</c> with no case analysis, where rounding would need the direction
/// argued. Since the expansion continues <c>...4944 5923...</c>, pi is in fact strictly inside
/// the interval at both ends, but the tests only ever rely on the closed containment.
/// </para>
/// </remarks>
internal static class PiReference
{
    /// <summary>The number of decimal places the expansion below is truncated at.</summary>
    public const int Places = 60;

    /// <summary>
    /// Pi to <see cref="Places"/> decimal places, truncated, written without the decimal point:
    /// the numerator over <c>10^60</c>.
    /// </summary>
    private const string TruncatedDigits =
        "3141592653589793238462643383279502884197169399375105820974944";

    /// <summary>The denominator the digits above sit over.</summary>
    private static readonly BigInteger Scale = BigInteger.Pow(10, Places);

    /// <summary>The truncated expansion itself. Pi is at or above this.</summary>
    public static BigRational Lower { get; } =
        new(BigInteger.Parse(TruncatedDigits, CultureInfo.InvariantCulture), Scale);

    /// <summary>One unit in the last place above <see cref="Lower"/>. Pi is at or below this.</summary>
    public static BigRational Upper { get; } = Lower + new BigRational(BigInteger.One, Scale);

    /// <summary>
    /// Determines whether an enclosure certainly contains pi, by containing the whole reference
    /// interval.
    /// </summary>
    /// <param name="enclosure">The enclosure to test.</param>
    /// <returns>
    /// <see langword="true"/> if every point the reference admits lies inside
    /// <paramref name="enclosure"/>.
    /// </returns>
    /// <remarks>
    /// The strong form, and the one a bound test wants: it is decidable from the reference alone,
    /// with no appeal to digits beyond the sixtieth. It can only be asked of an enclosure wider
    /// than <c>10^-60</c>; a finer one is tested with <see cref="LiesWithin"/> instead.
    /// </remarks>
    public static bool Pins(Approximation enclosure) =>
        enclosure.Lower <= Lower && Upper <= enclosure.Upper;

    /// <summary>
    /// Determines whether an enclosure is finer than the reference and sits entirely inside it.
    /// </summary>
    /// <param name="enclosure">The enclosure to test.</param>
    /// <returns><see langword="true"/> if the enclosure is contained in the reference interval.</returns>
    /// <remarks>
    /// The converse containment, for enclosures narrower than the sixtieth place. Asserting it is
    /// how the digit string above is checked against a computation rather than assumed.
    /// </remarks>
    public static bool LiesWithin(Approximation enclosure) =>
        Lower <= enclosure.Lower && enclosure.Upper <= Upper;

    /// <summary>
    /// Determines whether an enclosure is consistent with the reference, at any width.
    /// </summary>
    /// <param name="enclosure">The enclosure to test.</param>
    /// <returns><see langword="true"/> if the enclosure and the reference interval overlap.</returns>
    /// <remarks>
    /// The weak form, and the only one that applies at every width. A false here is a refutation:
    /// the enclosure and the reference cannot both hold, so the enclosure's bound is violated by
    /// more than <c>10^-60</c>.
    /// </remarks>
    public static bool AgreesWith(Approximation enclosure) =>
        enclosure.Lower <= Upper && Lower <= enclosure.Upper;
}
