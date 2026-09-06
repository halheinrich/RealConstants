using System.Globalization;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// The three zeta methods, named for the command line and constructed only as
/// <see cref="IRealConstant"/>.
/// </summary>
/// <remarks>
/// <para>
/// Everything downstream sees the interface and never a concrete type. That is not fastidiousness
/// - it is what keeps the comparison a comparison. A table that reached into one provider for a
/// figure it could not get from another would be measuring the reaching rather than the method.
/// Every column below comes from <c>ErrorBoundAt</c>, <c>Refinements()</c> and an oracle, which
/// is also why no provider needed a diagnostic hook to make this project possible.
/// </para>
/// </remarks>
internal static class Methods
{
    /// <summary>The command-line name for the central-binomial method.</summary>
    public const string CentralBinomial = "central";

    /// <summary>The command-line name for the Euler-Maclaurin method.</summary>
    public const string EulerMaclaurin = "euler";

    /// <summary>The command-line name for direct summation.</summary>
    public const string DirectSum = "direct";

    /// <summary>Every method name, in the order the tables use.</summary>
    public static string[] Names { get; } = [CentralBinomial, EulerMaclaurin, DirectSum];

    /// <summary>
    /// What a method is, in the terms a reader at the prompt needs: what it computes, over which
    /// orders, how fast, and what one step of it means.
    /// </summary>
    /// <param name="Name">The command-line name.</param>
    /// <param name="Provider">The type that implements it, for anyone wanting the proof.</param>
    /// <param name="Summary">A phrase naming the scheme.</param>
    /// <param name="Orders">Which values of <c>s</c> it serves.</param>
    /// <param name="Cadence">Roughly how much accuracy a step buys.</param>
    /// <param name="StepMeaning">What the step index counts.</param>
    internal sealed record Note(
        string Name,
        string Provider,
        string Summary,
        string Orders,
        string Cadence,
        string StepMeaning);

    /// <summary>
    /// The catalogue the listing and the walk's header are both built from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These strings restate what each provider's XML documentation already says, which is a
    /// duplication and is worth naming rather than hiding. It is admitted for one reason: XML
    /// documentation is not reachable at run time, and the alternative - a description property
    /// on <c>IRealConstant</c> or on each provider - is exactly the leak of an experiment's
    /// convenience into a ratified contract that this project refuses. So the text lives here,
    /// in one place rather than three, and every entry names its provider so a reader can reach
    /// the authoritative version.
    /// </para>
    /// <para>
    /// If a provider's identity or cadence changes, this table is stale and nothing will say so.
    /// That is the cost of the decision above, stated so the next person weighs it knowingly.
    /// </para>
    /// </remarks>
    public static Note[] Catalogue { get; } =
    [
        new(CentralBinomial, nameof(CentralBinomialZeta),
            "the central-binomial series",
            "2, 3, 4",
            "about 0.6 decimal digits per step",
            "step n is the partial sum over k = 1..n+1"),
        new(EulerMaclaurin, nameof(EulerMaclaurinZeta),
            "Euler-Maclaurin summation",
            "2 and up",
            "about 2.75 decimal digits per step",
            "step n uses N = n+2 exact terms, with the correction count chosen to minimise the bound"),
        new(DirectSum, nameof(DirectSumZeta),
            "direct summation of the definition",
            "2 and up",
            "the bound falls like N^-s, so a decimal digit costs a factor of 10^(1/s) in N "
            + "- about 3.2x at s = 2, 1.5x at s = 6",
            "step n is the partial sum over k = 1..n+1"),
    ];

    /// <summary>The identity a method computes at a given order, as a line of text.</summary>
    /// <param name="method">One of <see cref="Names"/>.</param>
    /// <param name="order">The order <c>s</c>, or zero for the generic form.</param>
    /// <returns>The identity, or a generic form when the order is not specific.</returns>
    /// <remarks>
    /// The central-binomial coefficient differs per order and s = 3 alternates, so that method's
    /// identity is only writable once the order is known. The other two are one formula in
    /// <c>s</c>.
    /// </remarks>
    public static string Identity(string method, int order)
    {
        if (string.Equals(method, CentralBinomial, StringComparison.OrdinalIgnoreCase))
        {
            return order switch
            {
                2 => "zeta(2) = 3 * sum over k >= 1 of 1/(k^2 * C(2k,k))",
                3 => "zeta(3) = (5/2) * sum over k >= 1 of (-1)^(k-1) / (k^3 * C(2k,k))",
                4 => "zeta(4) = (36/17) * sum over k >= 1 of 1/(k^4 * C(2k,k))",
                _ => "zeta(s) = c_s * sum over k >= 1 of (+/-) 1/(k^s * C(2k,k)), c_s = 3, 5/2, 36/17",
            };
        }

        // These two are one formula in s, and it stays in s. Substituting the order into the
        // left side alone gives "zeta(6) = ... k^-s ...", which reads as though the two sides
        // were about different things. The order is on the line above in the walk's header.
        return string.Equals(method, EulerMaclaurin, StringComparison.OrdinalIgnoreCase)
            ? "zeta(s) = sum over k < N of k^-s + N^(1-s)/(s-1) + N^-s/2 "
              + "+ sum over j of (B_2j/(2j)!) * (s)_(2j-1) * N^(1-s-2j)"
            : "zeta(s) = sum over k >= 1 of 1/k^s, tail bracketed by "
              + "(N+1)^(1-s)/(s-1) < tail < N^(1-s)/(s-1)";
    }

    /// <summary>Finds the catalogue entry for a method name.</summary>
    /// <param name="method">One of <see cref="Names"/>.</param>
    /// <returns>The entry, or <see langword="null"/> if the name is unknown.</returns>
    public static Note? Describe(string method) =>
        Array.Find(Catalogue, n => string.Equals(n.Name, method, StringComparison.OrdinalIgnoreCase));

    /// <summary>Builds a provider by method name and order.</summary>
    /// <param name="method">One of <see cref="Names"/>.</param>
    /// <param name="order">The order <c>s</c>.</param>
    /// <returns>The provider, or <see langword="null"/> if that method has no member at that order.</returns>
    public static IRealConstant? TryCreate(string method, int order)
    {
        try
        {
            if (string.Equals(method, CentralBinomial, StringComparison.OrdinalIgnoreCase))
            {
                return new CentralBinomialZeta(order);
            }

            if (string.Equals(method, EulerMaclaurin, StringComparison.OrdinalIgnoreCase))
            {
                return new EulerMaclaurinZeta(order);
            }

            return string.Equals(method, DirectSum, StringComparison.OrdinalIgnoreCase)
                ? new DirectSumZeta(order)
                : null;
        }
        catch (ArgumentOutOfRangeException)
        {
            // The central-binomial family has no member past s = 4, which is a fact about the
            // mathematics rather than an error here. A table cell simply says so.
            return null;
        }
    }

    /// <summary>
    /// An enclosure of zeta(s) far finer than anything a walk will reach, for reporting realised
    /// error.
    /// </summary>
    /// <param name="order">The order <c>s</c>.</param>
    /// <param name="method">The method being measured, which the oracle avoids where it can.</param>
    /// <returns>The oracle enclosure and a phrase naming what it is.</returns>
    /// <remarks>
    /// <para>
    /// Where a second method exists at this order, the oracle is that other method taken deep, so
    /// realised error is measured against something sharing no code with what is being measured.
    /// Where none exists - Euler-Maclaurin at <c>s = 6</c>, the central binomial family having no
    /// member there - it falls back to the same method taken far deeper, and says so.
    /// </para>
    /// <para>
    /// That fallback is honest for an experiment and would not be for a test. Here the column
    /// reports how fast a method converges, not whether it converges to the right number; a test
    /// asserting correctness against a deeper run of itself would be asserting nothing.
    /// </para>
    /// </remarks>
    public static (Approximation Enclosure, string Description) Oracle(int order, string method)
    {
        if (!string.Equals(method, EulerMaclaurin, StringComparison.OrdinalIgnoreCase))
        {
            return (new EulerMaclaurinZeta(order).Refinements().Skip(40).First(),
                    "Euler-Maclaurin at step 40");
        }

        if (order <= 4)
        {
            return (new CentralBinomialZeta(order).Refinements().Skip(200).First(),
                    "central binomial at step 200");
        }

        return (new EulerMaclaurinZeta(order).Refinements().Skip(60).First(),
                string.Create(CultureInfo.InvariantCulture,
                    $"Euler-Maclaurin at step 60 - the SAME method, deeper, since s={order} has no second one"));
    }
}
