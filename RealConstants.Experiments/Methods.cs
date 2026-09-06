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
