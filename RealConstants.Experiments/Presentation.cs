using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// Formatting helpers for the experiment tables: exact rationals rendered to decimal, and
/// magnitudes rendered as approximate decimal exponents.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the one place decimals are allowed, and only because it is the last one.</b>
/// <c>../AGENTS.md</c> § Exactness discipline bans floating point in a computational path and
/// permits formatting to decimal at presentation. Nothing here feeds back into a value or a
/// bound; every figure printed by an experiment is computed in exact rationals and passes
/// through this file on its way to the console and nowhere else.
/// </para>
/// <para>
/// The decimal conversion itself is exact truncation of an exact rational, not a floating-point
/// division: <c>numerator * 10^places / denominator</c> in integers, with the point inserted
/// afterwards. The <see cref="double"/> in <see cref="DecimalExponent"/> is the only one in the
/// repository, it estimates a magnitude for a column heading, and it is never compared against
/// anything.
/// </para>
/// </remarks>
internal static class Presentation
{
    /// <summary>Renders an exact rational to a fixed number of decimal places, truncated.</summary>
    /// <param name="value">The value.</param>
    /// <param name="places">How many places to show.</param>
    /// <returns>The truncated decimal rendering.</returns>
    public static string ToDecimal(BigRational value, int places)
    {
        BigInteger scale = BigInteger.Pow(10, places);
        BigInteger scaled = value.Numerator * scale / value.Denominator;

        bool negative = scaled.Sign < 0;
        BigInteger magnitude = BigInteger.Abs(scaled);

        string digits = (magnitude / scale).ToString(CultureInfo.InvariantCulture);
        string fraction = (magnitude % scale).ToString(CultureInfo.InvariantCulture).PadLeft(places, '0');

        return (negative ? "-" : string.Empty) + digits + "." + fraction;
    }

    /// <summary>
    /// The base-ten exponent of a positive rational, for a magnitude column.
    /// </summary>
    /// <param name="value">A strictly positive rational.</param>
    /// <returns><c>log10(value)</c>, to the accuracy of a <see cref="double"/>.</returns>
    /// <remarks>
    /// <para>
    /// The difference of the two <see cref="BigInteger.Log10(BigInteger)"/> values, which is
    /// exact to double precision at any size. Presentation only.
    /// </para>
    /// <para>
    /// This was a difference of bit lengths until 2026-09-05, and that estimate was documented
    /// as accurate to well under a digit, which it was. It was also systematically optimistic by
    /// up to 0.3, because a bit length rounds a logarithm up: a bound of exactly <c>1/6</c>, whose
    /// log10 is <c>-0.778</c>, printed as <c>1e-0.6</c> under a column headed <c>digits</c>. A
    /// documented estimate is not a false claim, but the honest figure costs the same, so there
    /// is no reason to print the estimate.
    /// </para>
    /// </remarks>
    public static double DecimalExponent(BigRational value)
    {
        if (value.Sign <= 0)
        {
            return double.NegativeInfinity;
        }

        return BigInteger.Log10(value.Numerator) - BigInteger.Log10(value.Denominator);
    }

    /// <summary>Renders a magnitude as a signed decimal exponent, e.g. <c>1e-27</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The rendering.</returns>
    public static string Magnitude(BigRational value) =>
        value.Sign <= 0
            ? "0"
            : string.Create(CultureInfo.InvariantCulture, $"1e{DecimalExponent(value):F1}");

    /// <summary>Renders how many correct decimal digits a bound corresponds to.</summary>
    /// <param name="bound">The bound.</param>
    /// <returns>The digit count, as text.</returns>
    public static string Digits(BigRational bound) =>
        bound.Sign <= 0
            ? "exact"
            : string.Create(CultureInfo.InvariantCulture, $"{-DecimalExponent(bound):F1}");

    /// <summary>Renders a ratio of two exact rationals to three decimal places.</summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator. Must be non-zero.</param>
    /// <returns>The rendering, or a dash when the denominator is zero.</returns>
    public static string Ratio(BigRational numerator, BigRational denominator) =>
        denominator.IsZero ? "-" : ToDecimal(numerator / denominator, 3);

    /// <summary>Renders a magnitude as a bare base-ten exponent, for a machine-readable column.</summary>
    /// <param name="value">The value.</param>
    /// <returns><c>-5.7</c>, or empty where the value is not strictly positive.</returns>
    /// <remarks>
    /// <see cref="Magnitude"/>'s <c>1e-5.7</c> is compact and readable and is <b>not a number</b>:
    /// no CSV consumer parses it, because scientific notation carries an integer exponent.
    /// Emitting the exponent alone is parseable and keeps the at-a-glance digit count that made
    /// the log form attractive in the first place, where <c>2.0e-6</c> would be parseable and
    /// lose it. An empty field rather than <c>-inf</c> for a zero bound: no provider here
    /// produces one, and a reader's tools will take a blank as missing rather than as a number
    /// they must special-case.
    /// </remarks>
    public static string Exponent(BigRational value) =>
        value.Sign <= 0
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"{DecimalExponent(value):F1}");

    /// <summary>
    /// Renders only the decimal digits an enclosure actually pins: the prefix its lower and upper
    /// bounds agree on.
    /// </summary>
    /// <param name="enclosure">The enclosure.</param>
    /// <param name="maxPlaces">How many decimal places to consider before giving up.</param>
    /// <returns>
    /// The shared prefix, <c>?</c> where the two bounds agree on nothing at all, and the prefix
    /// with a trailing <c>...</c> where it reaches <paramref name="maxPlaces"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <c>../VISION.md</c> § Guiding principles: <i>an unearned digit should be unrepresentable,
    /// not merely discouraged.</i> Rendering the value to a fixed forty places presented
    /// thirty-nine digits of noise as precision at step 0 of every walk, in the one tool whose
    /// job is showing what a method is doing.
    /// </para>
    /// <para>
    /// The prefix is taken character by character from the two <b>truncated</b> renderings, and
    /// truncation is the right rounding at both ends: a digit the two share after truncating is a
    /// digit they share exactly, and a digit truncation separates - <c>1.29999</c> against
    /// <c>1.30000</c> - is one the enclosure genuinely does not pin. Comparing the whole rendering
    /// rather than the fractional part is what makes <c>[9.9, 10.1]</c> come out as nothing
    /// earned: the integer parts differ in width, so no character position agrees, which is the
    /// truth. A count of digits derived from the bound would have said one.
    /// </para>
    /// <para>
    /// A prefix ending at the point - <c>1.</c> - is kept as it is. That is the honest rendering
    /// of an enclosure that pins the units digit and nothing after it, and it is what the first
    /// step of most walks earns. The column then grows as the method converges, so the panel
    /// shows convergence rather than describing it.
    /// </para>
    /// </remarks>
    public static string Earned(Approximation enclosure, int maxPlaces)
    {
        string low = ToDecimal(enclosure.Lower, maxPlaces);

        // An exact enclosure pins every digit it has, so there is no prefix to take.
        string high = enclosure.IsExact ? low : ToDecimal(enclosure.Upper, maxPlaces);

        int shared = 0;
        while (shared < low.Length && shared < high.Length && low[shared] == high[shared])
        {
            shared++;
        }

        if (shared == 0)
        {
            return "?";
        }

        return shared == low.Length && shared == high.Length
            ? low + "..."
            : low[..shared];
    }
}
