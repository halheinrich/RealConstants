using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// zeta(s) for integer <c>s &gt;= 2</c> by Euler-Maclaurin summation: a short exact partial sum,
/// an integral, and a run of Bernoulli corrections.
/// </summary>
/// <remarks>
/// <para>
/// For an integer <c>N &gt;= 2</c> and a correction count <c>M</c>,
/// </para>
/// <para>
/// <c>zeta(s) = sum over k &lt; N of k^-s + N^(1-s)/(s-1) + N^-s/2
/// + sum over j = 1..M of (B_2j/(2j)!) * (s)_(2j-1) * N^(1-s-2j) + R</c>,
/// </para>
/// <para>
/// where <c>(s)_m = s(s+1)...(s+m-1)</c> and <c>B_2j</c> are the Bernoulli numbers. Every
/// quantity is an exact rational. <b>Nothing here refers to pi</b>, directly or transitively -
/// which matters, because zeta(2n) is a rational multiple of pi^(2n), so a provider built from
/// pi would make <c>pi^(2n)/zeta(2n)</c> exact by construction and turn the positive controls
/// of <c>SPEC-rational-ratio.md</c> section 4 into tautologies. The Bernoulli numbers are
/// generated from their own recurrence, which is arithmetic on rationals and nothing more.
/// </para>
/// <para>
/// <b>The bound.</b> The formula above with its integral remainder is classical and is cited;
/// the bound on that remainder is derived here, in three steps a reader can check.
/// </para>
/// <para>
/// <b>1.</b> The remainder is
/// <c>R = -integral from N to infinity of (Bper_2M(x)/(2M)!) * f^(2M)(x) dx</c> with
/// <c>f(x) = x^-s</c>, where <c>Bper_2M</c> is the periodic Bernoulli function.
/// </para>
/// <para>
/// <b>2.</b> <c>|Bper_2M(x)| &lt;= |B_2M|</c> everywhere. Its Fourier series is a constant times
/// <c>sum over n &gt;= 1 of cos(2*pi*n*x)/n^(2M)</c>, and replacing every cosine by 1 gives the
/// value at <c>x = 0</c>, which is <c>|B_2M|</c>.
/// </para>
/// <para>
/// <b>3.</b> Pulling that maximum out leaves an integral that evaluates exactly. Since
/// <c>|f^(2M)(x)| = (s)_2M * x^(-s-2M)</c> and <c>(s)_2M = (s)_(2M-1) * (s+2M-1)</c>,
/// </para>
/// <para>
/// <c>|R| &lt;= (|B_2M|/(2M)!) * (s)_2M * N^(1-s-2M)/(s+2M-1) = (|B_2M|/(2M)!) * (s)_(2M-1) *
/// N^(1-s-2M)</c>,
/// </para>
/// <para>
/// which is exactly the magnitude of the <b>last term included</b>. So the bound is one term of
/// the sum that was already computed, and it needs no estimate of anything.
/// </para>
/// <para>
/// <b>The series in M is asymptotic and turns.</b> The terms fall, reach a smallest value near
/// <c>M = pi*N</c>, and then grow without limit - measured at <c>N = 10</c>, the realised error
/// improves to about <c>1.0e-27</c> at <c>M = 30</c> and then worsens to <c>1.8e-15</c> by
/// <c>M = 65</c>. <b>So a step may not grow M.</b> A step that did would report a shrinking
/// bound over a growing error, which is the one failure this bench exists to make impossible.
/// A test pins the turn rather than leaving the constraint to be remembered.
/// </para>
/// <para>
/// <b>A step grows N, and M is chosen rather than scheduled.</b> At each <c>N</c> this type
/// takes the <c>M</c> that <i>minimises the proven bound above</i>. That is not a heuristic: it
/// is the only defensible reading of an asymptotic series, and it makes the contract's
/// monotonicity obligation a two-line consequence rather than a measurement. Writing
/// <c>E(N,M)</c> for the bound and <c>M*(N)</c> for its minimiser,
/// </para>
/// <para>
/// <c>E(N+1, M*(N+1)) &lt;= E(N+1, M*(N)) &lt; E(N, M*(N))</c>,
/// </para>
/// <para>
/// the first step because <c>M*(N+1)</c> minimises at <c>N+1</c>, and the second because
/// <c>E(N, M)</c> is proportional to <c>N^(1-s-2M)</c> and so strictly falls as <c>N</c> grows
/// at fixed <c>M</c>. Tending to zero follows from the same second fact with <c>M</c> held at
/// its step-0 value.
/// </para>
/// <para>
/// The search for the minimiser scans upward from <c>M = 1</c> and stops where the bound first
/// fails to fall. That finds the global minimum because <c>E(N, M)</c> is unimodal in <c>M</c>:
/// consecutive bounds are in the ratio
/// <c>(zeta(2M+2)/zeta(2M)) * (s+2M-1)(s+2M) / (2*pi*N)^2</c>, in which both factors increase
/// with <c>M</c>, so the ratio crosses 1 once. (That identity mentions pi, but it is an
/// argument about the code rather than a step the code takes - no pi is computed anywhere.)
/// </para>
/// <para>
/// <b>About 2.7 decimal digits per step</b>, measured 2.746 for s = 2, 4 and 6 alike, which is
/// <c>log10(e^(2*pi))</c> - the classical accuracy of an optimally truncated Euler-Maclaurin.
/// That makes this much the fastest provider here, and it is why the exact partial sum stays
/// cheap: <c>N</c> reaches only about 25 for sixty digits, so the sum carries
/// <c>lcm(1..25)</c> rather than anything alarming. The measured cost sits in the Bernoulli
/// numbers instead, of which the minimiser needs about <c>pi*N</c>.
/// </para>
/// <para>
/// <b>The bound is tight to within a factor of two</b> at the minimising M: measured
/// realised-over-claimed of 0.49 to 0.51 at every <c>(s, N)</c> tested. That is close enough
/// that halving the bound is a borderline falsification rather than a decisive one, which the
/// tests state rather than paper over.
/// </para>
/// <para>
/// <b>No step guard.</b> The step index enters only as <c>N = step + 2</c>, carried in a
/// <see cref="long"/>, so nothing wraps and no index has to be refused. See
/// halheinrich/Math#53.
/// </para>
/// <para>
/// This provider shares no code with <see cref="CentralBinomialZeta"/> or
/// <see cref="DirectSumZeta"/>, which is what makes their agreement evidence rather than a
/// restatement.
/// </para>
/// <para>
/// <c>StepFor</c> and <c>ApproximateTo</c> are reachable only through an
/// <see cref="IRealConstant"/>-typed reference. They are default interface members, which C#
/// does not surface on the implementing type; this type deliberately does not re-declare them.
/// </para>
/// <para>
/// Instances carry no mutable state, so one may be shared freely and every member is
/// thread-safe. Each call to <see cref="Refinements"/> returns a fresh, independent sequence
/// with its own Bernoulli cache.
/// </para>
/// </remarks>
public sealed class EulerMaclaurinZeta : IRealConstant
{
    private const string OrderTooSmallMessage =
        "The order must be at least two. At s = 1 the series is the harmonic one and does not " +
        "converge, and the integral term N^(1-s)/(s-1) divides by zero.";

    /// <summary>Initialises a provider for zeta of the given order.</summary>
    /// <param name="order">The order <c>s</c>, at least two.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> is below two.</exception>
    public EulerMaclaurinZeta(int order)
    {
        if (order < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(order), order, OrderTooSmallMessage);
        }

        Order = order;
    }

    /// <summary>Gets the order <c>s</c> of the zeta value this provider approximates.</summary>
    public int Order { get; }

    /// <summary>
    /// Gets the proven upper bound on the error of step <paramref name="step"/>, without
    /// computing that step.
    /// </summary>
    /// <param name="step">The zero-based step index, matching the position in <see cref="Refinements"/>.</param>
    /// <returns>The least bound available at <c>N = step + 2</c>, over all correction counts.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="step"/> is negative.</exception>
    /// <remarks>
    /// This costs the Bernoulli numbers and the scan for the minimiser, and nothing else. The
    /// step additionally carries the exact partial sum over <c>k &lt; N</c>, whose denominator
    /// is a least common multiple, and forms every correction term rather than only their
    /// magnitudes. The gap is a factor rather than an order.
    /// </remarks>
    public BigRational ErrorBoundAt(int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);

        // Carried in long, so nothing wraps at the top of the int range.
        BigInteger count = (long)step + 2;
        List<BigRational> bernoulli = [];

        return ChooseCorrections(Order, count, bernoulli).Bound;
    }

    /// <summary>Gets the endless sequence of successively better enclosures of zeta(s).</summary>
    /// <returns>
    /// A lazy, endless sequence. Element <c>n</c> uses <c>N = n + 2</c> exact terms and the
    /// number of Bernoulli corrections that minimises the bound at that <c>N</c>.
    /// </returns>
    /// <remarks>
    /// Incremental where the scheme allows. The exact partial sum gains one reciprocal power a
    /// step and is never resummed, and the Bernoulli numbers are computed once each and kept.
    /// The correction sum itself is rebuilt every step, because the number of corrections grows
    /// with <c>N</c> and each term depends on <c>N</c> - the same limit
    /// <see cref="BorweinZetaThree"/> runs into, and for the same reason.
    /// </remarks>
    public IEnumerable<Approximation> Refinements()
    {
        List<BigRational> bernoulli = [];
        BigRational partial = BigRational.Zero;
        BigInteger count = 2;

        // The partial sum runs over k < N, so at N = 2 it is the single term 1/1^s.
        partial += BigRational.One;

        while (true)
        {
            (int corrections, BigRational bound) = ChooseCorrections(Order, count, bernoulli);

            BigRational value = partial
                + new BigRational(BigInteger.One, (Order - 1) * BigInteger.Pow(count, Order - 1))
                + new BigRational(BigInteger.One, 2 * BigInteger.Pow(count, Order));

            for (int j = 1; j <= corrections; j++)
            {
                value += CorrectionTerm(Order, count, j, bernoulli);
            }

            yield return Approximation.Create(value, bound);

            partial += new BigRational(BigInteger.One, BigInteger.Pow(count, Order));
            count += BigInteger.One;
        }
    }

    /// <summary>
    /// Finds the correction count minimising the proven bound at the given exact-term count.
    /// </summary>
    /// <param name="order">The order <c>s</c>.</param>
    /// <param name="count">The exact-term count <c>N</c>.</param>
    /// <param name="bernoulli">A cache of even-index Bernoulli numbers, extended as needed.</param>
    /// <returns>The minimising correction count and the bound it attains.</returns>
    /// <remarks>
    /// Scans upward and stops where the bound first fails to fall. The bound is unimodal in the
    /// correction count, so that is the global minimum; the argument is in this type's remarks.
    /// </remarks>
    private static (int Corrections, BigRational Bound) ChooseCorrections(
        int order, BigInteger count, List<BigRational> bernoulli)
    {
        int best = 1;
        BigRational bound = BigRational.Abs(CorrectionTerm(order, count, 1, bernoulli));

        for (int j = 2; ; j++)
        {
            BigRational candidate = BigRational.Abs(CorrectionTerm(order, count, j, bernoulli));
            if (candidate >= bound)
            {
                return (best, bound);
            }

            best = j;
            bound = candidate;
        }
    }

    /// <summary>Builds the <c>j</c>-th Euler-Maclaurin correction term.</summary>
    /// <param name="order">The order <c>s</c>.</param>
    /// <param name="count">The exact-term count <c>N</c>.</param>
    /// <param name="j">The one-based correction index.</param>
    /// <param name="bernoulli">A cache of even-index Bernoulli numbers, extended as needed.</param>
    /// <returns><c>(B_2j/(2j)!) * (s)_(2j-1) * N^(1-s-2j)</c>.</returns>
    private static BigRational CorrectionTerm(
        int order, BigInteger count, int j, List<BigRational> bernoulli)
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

        BigRational scale = new(BigInteger.One, factorial * BigInteger.Pow(count, order + (2 * j) - 1));

        return EvenBernoulli(j, bernoulli) * rising * scale;
    }

    /// <summary>Gets <c>B_(2j)</c>, extending the cache as far as needed.</summary>
    /// <param name="j">The half-index, at least one.</param>
    /// <param name="cache">Even-index Bernoulli numbers already computed, <c>B_2</c> onward.</param>
    /// <returns><c>B_(2j)</c>.</returns>
    /// <remarks>
    /// <para>
    /// From the defining recurrence <c>sum over i = 0..m of C(m+1,i) * B_i = 0</c>, which fixes
    /// <c>B_m</c> once the earlier ones are known. Odd-index values above the first vanish and
    /// are not stored, but <c>B_1 = -1/2</c> is needed inside the sum and is supplied inline.
    /// </para>
    /// <para>
    /// The cache is a parameter rather than a static field on purpose. A shared cache would be
    /// mutable state on a type whose documentation promises thread safety, and the promise is
    /// worth more than the recomputation.
    /// </para>
    /// </remarks>
    private static BigRational EvenBernoulli(int j, List<BigRational> cache)
    {
        while (cache.Count < j)
        {
            int m = 2 * (cache.Count + 1);

            // sum over i < m of C(m+1, i) * B_i, with C advanced multiplicatively.
            BigRational total = BigRational.One;
            BigInteger binomial = m + 1;
            total += binomial * new BigRational(-1, 2);

            for (int i = 2; i < m; i++)
            {
                binomial = binomial * (m + 2 - i) / i;
                if (i % 2 == 0)
                {
                    total += binomial * cache[(i / 2) - 1];
                }
            }

            cache.Add(-total / (m + 1));
        }

        return cache[j - 1];
    }
}
