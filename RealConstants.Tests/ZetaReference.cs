using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// zeta(s) as an external reference for <c>s</c> in 2, 3, 4 and 6: the decimal expansion
/// truncated at sixty places, held as the exact rational interval that truncation licenses.
/// </summary>
/// <remarks>
/// <para>
/// This is a hardcoded digit string, which the design deliberately prefers <i>not</i> to lean on
/// where a cross-check between independent providers will do. It is here because the two jobs
/// are different: a cross-check establishes that two providers agree, not that they agree on
/// zeta(s), and it cannot check a bound at a precision the coarser provider never reaches.
/// </para>
/// <para>
/// The digits are not taken on trust, and they are checked from two directions that share
/// nothing with each other. <see cref="DirectSumZeta"/> - the definition itself, with a proven
/// tail bracket and no identity, coefficient or acceleration anywhere in it - fixes the leading
/// places from below the whole edifice. And a deep enclosure from each fast provider, with a
/// proven bound far finer than the sixtieth place, must lie wholly inside the interval these
/// digits license. A mistyped digit moves that interval by at least <c>10^-60</c> and fails the
/// second; a wrong digit early enough fails the first as well.
/// </para>
/// <para>
/// One type for four constants rather than one per constant. The rule encoded here - truncated
/// digits, held as the interval truncation licenses, with the three containment predicates - is
/// a single decision, and <c>../AGENTS.md</c> section Writing code makes the same rule in four
/// places a defect. The per-order facts are data: one digit string each.
/// </para>
/// <para>
/// Truncated rather than rounded, on purpose: truncation puts the true value in
/// <c>[Lower, Lower + 10^-60)</c> with no case analysis, where rounding would need the
/// direction argued.
/// </para>
/// </remarks>
internal sealed class ZetaReference
{
    /// <summary>The number of decimal places the expansions below are truncated at.</summary>
    public const int Places = 60;

    /// <summary>
    /// The truncated expansions, written without the decimal point: the numerator over
    /// <c>10^60</c>. Each was derived from two agreeing exact-rational computations rather than
    /// transcribed, and each is checked by the tests before anything is tested against it.
    /// </summary>
    private static readonly Dictionary<int, string> TruncatedDigits = new()
    {
        [2] = "1644934066848226436472415166646025189218949901206798437735558",
        [3] = "1202056903159594285399738161511449990764986292340498881792271",
        [4] = "1082323233711138191516003696541167902774750951918726907682976",
        [6] = "1017343061984449139714517929790920527901817490032853561842408",
    };

    private static readonly BigInteger Scale = BigInteger.Pow(10, Places);

    private ZetaReference(int order, string digits)
    {
        Order = order;
        Lower = new BigRational(BigInteger.Parse(digits, CultureInfo.InvariantCulture), Scale);
        Upper = Lower + new BigRational(BigInteger.One, Scale);
    }

    /// <summary>Gets the order <c>s</c> this reference is for.</summary>
    public int Order { get; }

    /// <summary>Gets the truncated expansion itself. zeta(s) is at or above this.</summary>
    public BigRational Lower { get; }

    /// <summary>Gets one unit in the last place above <see cref="Lower"/>. zeta(s) is at or below this.</summary>
    public BigRational Upper { get; }

    /// <summary>Gets the reference for an order this type carries digits for.</summary>
    /// <param name="order">2, 3, 4 or 6.</param>
    /// <returns>The reference interval.</returns>
    /// <exception cref="ArgumentOutOfRangeException">No digits are held for that order.</exception>
    public static ZetaReference For(int order) =>
        TruncatedDigits.TryGetValue(order, out string? digits)
            ? new ZetaReference(order, digits)
            : throw new ArgumentOutOfRangeException(
                nameof(order), order, "No reference expansion is held for that order.");

    /// <summary>
    /// Determines whether an enclosure certainly contains zeta(s), by containing the whole
    /// reference interval.
    /// </summary>
    /// <param name="enclosure">The enclosure to test.</param>
    /// <returns><see langword="true"/> if every point the reference admits lies inside it.</returns>
    /// <remarks>
    /// The strong form, and the one a bound test wants. It can only be asked of an enclosure
    /// wider than <c>10^-60</c>; a finer one is tested with <see cref="LiesWithin"/> instead.
    /// </remarks>
    public bool Pins(Approximation enclosure) =>
        enclosure.Lower <= Lower && Upper <= enclosure.Upper;

    /// <summary>Determines whether an enclosure is finer than the reference and sits inside it.</summary>
    /// <param name="enclosure">The enclosure to test.</param>
    /// <returns><see langword="true"/> if the enclosure is contained in the reference interval.</returns>
    /// <remarks>
    /// The converse containment, for enclosures narrower than the sixtieth place. Asserting it
    /// is how the digit string above is checked against a computation rather than assumed.
    /// </remarks>
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
