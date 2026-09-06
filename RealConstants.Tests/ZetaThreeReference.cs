using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// zeta(3) as an external reference, in two forms: the decimal expansion truncated at sixty
/// places, and an enclosure computed from the defining series with a proven tail bound.
/// </summary>
/// <remarks>
/// <para>
/// The two forms exist because neither alone is a good oracle. The digit string reaches sixty
/// places but is only as good as what was typed. The defining series reaches about nine places
/// but is <b>proved from the definition of zeta(3) and nothing else</b>, so it can ground the
/// leading digits without any appeal to a provider or to a typed value.
/// </para>
/// <para>
/// Together they close the loop the pi pair's <c>PiReference</c> leaves slightly open. There,
/// the digits are checked only by a deep provider enclosure, which is sound but means the
/// string and the providers vouch for each other. Here <see cref="FromDefinition"/> is a third
/// party that shares nothing with either provider - it is the series
/// <c>sum of 1/k^3</c> itself - and it independently fixes the first nine places.
/// </para>
/// <para>
/// Truncated rather than rounded, on purpose: truncation puts the true value in
/// <c>[Lower, Lower + 10^-60)</c> with no case analysis, where rounding would need the
/// direction argued.
/// </para>
/// </remarks>
internal static class ZetaThreeReference
{
    /// <summary>The number of decimal places the expansion below is truncated at.</summary>
    public const int Places = 60;

    /// <summary>
    /// zeta(3) to <see cref="Places"/> decimal places, truncated, written without the decimal
    /// point: the numerator over <c>10^60</c>.
    /// </summary>
    private const string TruncatedDigits =
        "1202056903159594285399738161511449990764986292340498881792271";

    /// <summary>The denominator the digits above sit over.</summary>
    private static readonly BigInteger Scale = BigInteger.Pow(10, Places);

    /// <summary>The truncated expansion itself. zeta(3) is at or above this.</summary>
    public static BigRational Lower { get; } =
        new(BigInteger.Parse(TruncatedDigits, CultureInfo.InvariantCulture), Scale);

    /// <summary>One unit in the last place above <see cref="Lower"/>. zeta(3) is at or below this.</summary>
    public static BigRational Upper { get; } = Lower + new BigRational(BigInteger.One, Scale);

    /// <summary>
    /// Encloses zeta(3) using only its definition, <c>sum over k &gt;= 1 of 1/k^3</c>, with a
    /// proven two-sided bound on the tail.
    /// </summary>
    /// <param name="terms">How many terms to sum. Must be positive.</param>
    /// <returns>An enclosure of zeta(3) of half-width about <c>1/(2*terms^3)</c>.</returns>
    /// <remarks>
    /// <para>
    /// The tail is bracketed by integrals, both directions elementary because <c>x^-3</c> is
    /// decreasing. For <c>k &gt;= N+1</c>, <c>1/k^3</c> is below the integral of <c>x^-3</c>
    /// over <c>[k-1, k]</c> and above its integral over <c>[k, k+1]</c>. Summing over
    /// <c>k &gt; N</c> telescopes both sides into
    /// </para>
    /// <para>
    /// <c>1/(2*(N+1)^2) &lt; tail &lt; 1/(2*N^2)</c>,
    /// </para>
    /// <para>
    /// so zeta(3) lies strictly between <c>S_N + 1/(2*(N+1)^2)</c> and <c>S_N + 1/(2*N^2)</c>.
    /// This returns the midpoint of that interval with half its width, which is a valid
    /// enclosure and a slightly conservative one, since the true value is strictly inside.
    /// </para>
    /// <para>
    /// Nothing here is shared with either provider: no binomial coefficients, no Chebyshev
    /// weights, no acceleration of any kind. It is the slow definition, which is exactly what
    /// makes it worth having.
    /// </para>
    /// </remarks>
    public static Approximation FromDefinition(int terms)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(terms);

        BigRational partial = BigRational.Zero;
        for (int k = 1; k <= terms; k++)
        {
            BigInteger big = k;
            partial += new BigRational(BigInteger.One, big * big * big);
        }

        BigInteger n = terms;
        BigRational tailUpper = new(BigInteger.One, 2 * n * n);
        BigRational tailLower = new(BigInteger.One, 2 * (n + BigInteger.One) * (n + BigInteger.One));

        BigRational low = partial + tailLower;
        BigRational high = partial + tailUpper;

        return Approximation.Create((low + high) / 2, (high - low) / 2);
    }

    /// <summary>
    /// Determines whether an enclosure certainly contains zeta(3), by containing the whole
    /// reference interval.
    /// </summary>
    /// <param name="enclosure">The enclosure to test.</param>
    /// <returns><see langword="true"/> if every point the reference admits lies inside it.</returns>
    /// <remarks>
    /// The strong form, and the one a bound test wants. It can only be asked of an enclosure
    /// wider than <c>10^-60</c>; a finer one is tested with <see cref="LiesWithin"/> instead.
    /// </remarks>
    public static bool Pins(Approximation enclosure) =>
        enclosure.Lower <= Lower && Upper <= enclosure.Upper;

    /// <summary>Determines whether an enclosure is finer than the reference and sits inside it.</summary>
    /// <param name="enclosure">The enclosure to test.</param>
    /// <returns><see langword="true"/> if the enclosure is contained in the reference interval.</returns>
    /// <remarks>
    /// The converse containment, for enclosures narrower than the sixtieth place. Asserting it
    /// is how the digit string above is checked against a computation rather than assumed.
    /// </remarks>
    public static bool LiesWithin(Approximation enclosure) =>
        Lower <= enclosure.Lower && enclosure.Upper <= Upper;

    /// <summary>Determines whether an enclosure is consistent with the reference, at any width.</summary>
    /// <param name="enclosure">The enclosure to test.</param>
    /// <returns><see langword="true"/> if the enclosure and the reference interval overlap.</returns>
    /// <remarks>
    /// The weak form, and the only one that applies at every width. A false here is a
    /// refutation: the enclosure and the reference cannot both hold.
    /// </remarks>
    public static bool AgreesWith(Approximation enclosure) =>
        enclosure.Lower <= Upper && Lower <= enclosure.Upper;
}
