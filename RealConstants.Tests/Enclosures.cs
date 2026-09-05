using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// Predicates on pairs of enclosures, and the small helpers the tests build inputs from.
/// </summary>
/// <remarks>
/// <see cref="Approximation"/> deliberately exposes decidable predicates rather than an ordering,
/// because two overlapping enclosures have no defined order. These are the two relations the
/// tests here need and the type does not carry: one enclosure meeting another, and one containing
/// another.
/// </remarks>
internal static class Enclosures
{
    /// <summary>Determines whether two enclosures are consistent with each other.</summary>
    /// <param name="left">The first enclosure.</param>
    /// <param name="right">The second enclosure.</param>
    /// <returns><see langword="true"/> if the two intervals overlap.</returns>
    /// <remarks>
    /// The cross-check's whole assertion. Two providers of the same constant must produce
    /// overlapping intervals, because the truth is in both; disjoint intervals refute at least
    /// one of the two bounds without saying which.
    /// </remarks>
    public static bool Meet(Approximation left, Approximation right) =>
        left.Lower <= right.Upper && right.Lower <= left.Upper;

    /// <summary>Determines whether one enclosure lies entirely inside another.</summary>
    /// <param name="outer">The enclosure expected to be the wider one.</param>
    /// <param name="inner">The enclosure expected to sit inside it.</param>
    /// <returns><see langword="true"/> if <paramref name="inner"/> is contained in <paramref name="outer"/>.</returns>
    /// <remarks>
    /// Strictly stronger than <see cref="Meet"/>, and available whenever one provider is the more
    /// precise of the two. Where it applies it is the assertion to make: overlap tolerates a
    /// disagreement that consumes almost the whole of both bounds, containment does not.
    /// </remarks>
    public static bool Contain(Approximation outer, Approximation inner) =>
        outer.Lower <= inner.Lower && inner.Upper <= outer.Upper;

    /// <summary>Builds an exact rational from two integers.</summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator.</param>
    /// <returns>The rational.</returns>
    public static BigRational Ratio(int numerator, int denominator) =>
        new(numerator, denominator);

    /// <summary>Formats an assertion message invariantly.</summary>
    /// <param name="message">The interpolated message.</param>
    /// <returns>The formatted string.</returns>
    /// <remarks>
    /// Test output is read by whoever is looking at a failure, not by a locale. Formatting it
    /// invariantly keeps a failing assertion's numbers identical wherever the suite runs.
    /// </remarks>
    public static string Inv(FormattableString message) => FormattableString.Invariant(message);

    /// <summary>Builds ten raised to a negative power, exactly.</summary>
    /// <param name="places">How many decimal places down. Must not be negative.</param>
    /// <returns><c>10^-places</c> as an exact rational.</returns>
    public static BigRational TenToTheMinus(int places)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(places);
        return new BigRational(BigInteger.One, BigInteger.Pow(10, places));
    }
}
