using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The Euler-Maclaurin formula for zeta(s) at an <b>explicitly chosen</b> exact-term count and
/// correction count, rebuilt from scratch each call.
/// </summary>
/// <remarks>
/// <para>
/// This exists because <see cref="EulerMaclaurinZeta"/> deliberately cannot be asked for a
/// correction count. It picks the one that minimises its bound, which is what keeps it on the
/// safe side of the series' turn - and that is precisely the property under test, so the test
/// cannot go through the provider to establish it.
/// </para>
/// <para>
/// It is a second implementation of the same formula, which the single-source rule would
/// normally forbid. It is admissible here for the reason <c>../AGENTS.md</c> section Writing
/// code gives: the duplication encodes an independent decision rather than the same rule twice.
/// The provider's job is to choose a correction count and never leave the safe region; this
/// helper's job is to enter the unsafe region on purpose. Neither can do the other's.
/// </para>
/// <para>
/// It is also, incidentally, the from-scratch route the provider's incremental one is checked
/// against - the provider carries a partial sum and a Bernoulli cache across steps, and this
/// rebuilds both every call, so the two agreeing is evidence.
/// </para>
/// </remarks>
internal static class EulerMaclaurin
{
    /// <summary>
    /// Evaluates the Euler-Maclaurin approximation to zeta(s) at the given counts.
    /// </summary>
    /// <param name="order">The order <c>s</c>, at least two.</param>
    /// <param name="count">The exact-term count <c>N</c>, at least two.</param>
    /// <param name="corrections">The correction count <c>M</c>, at least zero.</param>
    /// <returns>The approximation, exactly.</returns>
    public static BigRational Value(int order, int count, int corrections)
    {
        BigRational total = BigRational.Zero;
        for (int k = 1; k < count; k++)
        {
            total += new BigRational(BigInteger.One, BigInteger.Pow(k, order));
        }

        total += new BigRational(BigInteger.One, (order - 1) * BigInteger.Pow(count, order - 1));
        total += new BigRational(BigInteger.One, 2 * BigInteger.Pow(count, order));

        for (int j = 1; j <= corrections; j++)
        {
            total += Term(order, count, j);
        }

        return total;
    }

    /// <summary>Gets the proven bound at the given counts: the magnitude of the last term included.</summary>
    /// <param name="order">The order <c>s</c>.</param>
    /// <param name="count">The exact-term count <c>N</c>.</param>
    /// <param name="corrections">The correction count <c>M</c>, at least one.</param>
    /// <returns>The bound.</returns>
    public static BigRational Bound(int order, int count, int corrections) =>
        BigRational.Abs(Term(order, count, corrections));

    /// <summary>Finds the correction count minimising <see cref="Bound"/> at the given exact-term count.</summary>
    /// <param name="order">The order <c>s</c>.</param>
    /// <param name="count">The exact-term count <c>N</c>.</param>
    /// <returns>The minimising correction count.</returns>
    public static int MinimisingCorrections(int order, int count)
    {
        int best = 1;
        BigRational least = Bound(order, count, 1);

        for (int j = 2; ; j++)
        {
            BigRational candidate = Bound(order, count, j);
            if (candidate >= least)
            {
                return best;
            }

            best = j;
            least = candidate;
        }
    }

    /// <summary>The <c>j</c>-th correction term, <c>(B_2j/(2j)!) * (s)_(2j-1) * N^(1-s-2j)</c>.</summary>
    private static BigRational Term(int order, int count, int j)
    {
        BigRational rising = BigRational.One;
        for (int i = 0; i < (2 * j) - 1; i++)
        {
            rising *= order + i;
        }

        BigInteger factorial = BigInteger.One;
        for (int i = 2; i <= 2 * j; i++)
        {
            factorial *= i;
        }

        BigRational scale = new(
            BigInteger.One,
            factorial * BigInteger.Pow(count, order + (2 * j) - 1));

        return Bernoulli(2 * j) * rising * scale;
    }

    /// <summary>
    /// <c>B_n</c> from the recurrence <c>sum over i = 0..m of C(m+1,i) * B_i = 0</c>, with
    /// <c>B_1 = -1/2</c>.
    /// </summary>
    private static BigRational Bernoulli(int n)
    {
        BigRational[] values = new BigRational[n + 1];
        values[0] = BigRational.One;

        for (int m = 1; m <= n; m++)
        {
            BigRational total = BigRational.Zero;
            BigInteger binomial = BigInteger.One;

            for (int i = 0; i < m; i++)
            {
                if (i > 0)
                {
                    binomial = binomial * (m + 2 - i) / i;
                }

                total += binomial * values[i];
            }

            values[m] = -total / (m + 1);
        }

        return values[n];
    }
}
