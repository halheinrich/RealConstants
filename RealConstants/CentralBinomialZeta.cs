using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// zeta(s) for <c>s</c> in 2, 3 and 4, by the central-binomial series
/// <c>zeta(s) = c_s * sum over k &gt;= 1 of (+/-) 1 / (k^s * C(2k,k))</c>.
/// </summary>
/// <remarks>
/// <para>
/// Step <c>n</c> is the partial sum over <c>k = 1 .. n+1</c>. The three members of the family
/// differ only in a leading coefficient and in whether the series alternates:
/// </para>
/// <list type="bullet">
/// <item><description><c>zeta(2) = 3 * sum 1/(k^2 C(2k,k))</c></description></item>
/// <item><description><c>zeta(3) = (5/2) * sum (-1)^(k-1)/(k^3 C(2k,k))</c> - Hjortnaes 1953,
/// and the series <b>Apery</b> used in 1978 to prove zeta(3) irrational</description></item>
/// <item><description><c>zeta(4) = (36/17) * sum 1/(k^4 C(2k,k))</c></description></item>
/// </list>
/// <para>
/// The identities are classical and are <b>cited, not proved here</b>. What is proved here is
/// the error bound. Each identity is checked the way any external claim is: the tests enclose an
/// independently computed value, and the cross-check against <see cref="EulerMaclaurinZeta"/>
/// would refute a wrong coefficient at once.
/// </para>
/// <para>
/// <b>The family stops at s = 4, and that is measured rather than unexplored.</b>
/// <c>zeta(6) / sum 1/(k^6 C(2k,k))</c> is <c>2.02385...</c>, which is not a rational with any
/// small height, so there is no coefficient to write. A larger <c>s</c> is refused by
/// <see cref="ErrorBoundAt"/>'s constructor guard rather than by the type system, matching
/// <see cref="NewtonSquareRoot"/>'s treatment of radicands: the domain is a fact about the
/// mathematics, not a shape the compiler can carry.
/// </para>
/// <para>
/// <b>One inequality bounds the whole family.</b> Write <c>a_k = 1/(k^s C(2k,k))</c>. Since
/// <c>C(2k+2,k+1) = C(2k,k) * 2*(2k+1)/(k+1)</c>, consecutive terms are in the ratio
/// </para>
/// <para>
/// <c>a_(k+1)/a_k = k^s / ((k+1)^(s-1) * 2 * (2k+1))</c>.
/// </para>
/// <para>
/// That is below <c>1/4</c> for every <c>k &gt;= 1</c> and every <c>s &gt;= 2</c>, in one line:
/// the claim is <c>4*k^s &lt; 2*(k+1)^(s-1)*(2k+1)</c>, and since <c>(k+1)^(s-1) &gt;=
/// k^(s-1)</c> the right side is at least <c>2*k^(s-1)*(2k+1) = 4*k^s + 2*k^(s-1)</c>, which
/// exceeds the left. So the terms are positive, strictly decreasing, and tend to zero at least
/// geometrically - all three obligations from the one inequality.
/// </para>
/// <para>
/// <b>Two bounds follow from it, and each s keeps the tighter one it has earned.</b> For the
/// alternating member the series is alternating in the strict sense, so the remainder is at most
/// the first omitted term - the grouping argument is written out in full in
/// <see cref="LeibnizPi"/> and is the same one here. For the two positive members every term is
/// added, so the tail is bounded by the geometric series it dominates:
/// <c>a_(n+2) * (1 + 1/4 + 1/16 + ...) = a_(n+2) * 4/3</c>. Hence
/// </para>
/// <para>
/// <c>bound = c_s * a_(n+2) * (alternating ? 1 : 4/3)</c>.
/// </para>
/// <para>
/// Flattening both cases to the geometric form would be sound and would widen the alternating
/// member's bound by exactly <c>4/3</c> at every step. That is discarding a theorem for
/// symmetry, so it is not done; the per-s facts are data - three coefficients and one flag -
/// and the two bound shapes are two arguments, not one written twice.
/// </para>
/// <para>
/// <b>About 0.6 decimal digits per step</b>, since the ratio tends to <c>1/4</c> and
/// <c>log10 4</c> is <c>0.602</c>. Measured 0.62 to 0.64 over the first hundred steps, the
/// excess coming from the <c>k^s</c> factor.
/// </para>
/// <para>
/// <b>No step guard.</b> The step index enters only as <c>k = step + 2</c>, carried in a
/// <see cref="long"/>, so nothing wraps and no index has to be refused. An absurd step is slow
/// rather than wrong. See halheinrich/Math#53 for why an avoidable guard is worth avoiding.
/// </para>
/// <para>
/// This provider shares no code with <see cref="EulerMaclaurinZeta"/> or
/// <see cref="DirectSumZeta"/>, which is what makes their agreement evidence.
/// <c>SPEC-rational-ratio.md</c> section 4 ratifies that as a rule: a defect in a shared engine
/// would move both values the same way, leaving them agreeing inside their bounds at exactly
/// the moment the check was needed. It is also why <c>s</c> is a constructor parameter and the
/// <i>method</i> is not - different constants are never cross-checked against each other, so
/// one type may serve several of them, while two methods for one constant must not share a
/// step convention.
/// </para>
/// <para>
/// <c>StepFor</c> and <c>ApproximateTo</c> are reachable only through an
/// <see cref="IRealConstant"/>-typed reference. They are default interface members, which C#
/// does not surface on the implementing type; this type deliberately does not re-declare them.
/// </para>
/// <para>
/// Instances carry no state, so one may be shared freely and every member is thread-safe. Each
/// call to <see cref="Refinements"/> returns a fresh, independent sequence.
/// </para>
/// </remarks>
public sealed class CentralBinomialZeta : IRealConstant
{
    private const string OrderOutOfFamilyMessage =
        "The central-binomial series for zeta(s) has a rational leading coefficient only for " +
        "s = 2, 3 and 4. At s = 6 the ratio zeta(6) / sum 1/(k^6 C(2k,k)) is 2.02385..., which " +
        "is not such a coefficient, so the family ends there. This is a measured dead end, not " +
        "an unimplemented case.";

    /// <summary>The geometric tail factor <c>1 + 1/4 + 1/16 + ... = 4/3</c>.</summary>
    private static readonly BigRational GeometricTail = new(4, 3);

    /// <summary>The leading coefficient <c>c_s</c>.</summary>
    private readonly BigRational _coefficient;

    /// <summary>Whether this member's series alternates in sign.</summary>
    private readonly bool _alternating;

    /// <summary>Initialises a provider for zeta of the given order.</summary>
    /// <param name="order">The order <c>s</c>: 2, 3 or 4.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="order"/> is not one of 2, 3 or 4.
    /// </exception>
    public CentralBinomialZeta(int order)
    {
        (_coefficient, _alternating) = order switch
        {
            2 => (new BigRational(3, 1), false),
            3 => (new BigRational(5, 2), true),
            4 => (new BigRational(36, 17), false),
            _ => throw new ArgumentOutOfRangeException(nameof(order), order, OrderOutOfFamilyMessage),
        };

        Order = order;
    }

    /// <summary>Gets the order <c>s</c> of the zeta value this provider approximates.</summary>
    public int Order { get; }

    /// <summary>
    /// Gets the proven upper bound on the error of step <paramref name="step"/>, without
    /// computing that step.
    /// </summary>
    /// <param name="step">The zero-based step index, matching the position in <see cref="Refinements"/>.</param>
    /// <returns>
    /// <c>c_s / (k^s * C(2k,k))</c> with <c>k = step + 2</c>, times <c>4/3</c> when the series
    /// does not alternate.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="step"/> is negative.</exception>
    /// <remarks>
    /// One central binomial coefficient, where the step itself is <c>step + 1</c> rational
    /// additions on denominators that grow without limit, each renormalised by a greatest
    /// common divisor. The gap is a factor rather than an order, but it is real.
    /// </remarks>
    public BigRational ErrorBoundAt(int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);

        // Carried in long, not int. At step = int.MaxValue the index would wrap and the bound
        // would come back larger than step 0's, breaking the non-increasing obligation at
        // exactly the step a bracketing StepFor reaches last and trusts most.
        long firstOmitted = (long)step + 2;

        BigRational term = _coefficient * new BigRational(
            BigInteger.One,
            BigInteger.Pow(firstOmitted, Order) * CentralBinomial(firstOmitted));

        return _alternating ? term : term * GeometricTail;
    }

    /// <summary>Gets the endless sequence of successively better enclosures of zeta(s).</summary>
    /// <returns>
    /// A lazy, endless sequence. Element <c>n</c> holds the partial sum over
    /// <c>k = 1 .. n+1</c>, scaled by <c>c_s</c>, with the <see cref="ErrorBoundAt"/> bound for
    /// that step.
    /// </returns>
    /// <remarks>
    /// Incremental in both the sum and the binomial coefficient: each element adds one term to
    /// the previous partial sum and advances <c>C(2k,k)</c> by its own recurrence, rather than
    /// recomputing the coefficient from scratch. The term index is carried as a
    /// <see cref="BigInteger"/> rather than a step counter, so the sequence is endless in fact
    /// and not merely in intent.
    /// </remarks>
    public IEnumerable<Approximation> Refinements()
    {
        BigRational sum = BigRational.Zero;

        // The current term's index k, the central binomial C(2k,k) at that index, and whether
        // the term is added or subtracted. At k = 1 the coefficient is C(2,1) = 2.
        BigInteger index = BigInteger.One;
        BigInteger central = 2;
        bool add = true;

        while (true)
        {
            BigRational term = new(BigInteger.One, BigInteger.Pow(index, Order) * central);
            sum = add ? sum + term : sum - term;
            add = !_alternating || !add;

            // Advance to the next index. C(2k+2,k+1) = C(2k,k) * 2*(2k+1)/(k+1), and the
            // division is exact because its quotient is that binomial coefficient, an integer.
            central = central * 2 * ((2 * index) + 1) / (index + BigInteger.One);
            index += BigInteger.One;

            // index now names the first omitted term. That agrees with ErrorBoundAt by
            // construction: at step n this index is n+2.
            BigRational bound = _coefficient * new BigRational(
                BigInteger.One,
                BigInteger.Pow(index, Order) * central);

            yield return Approximation.Create(
                _coefficient * sum,
                _alternating ? bound : bound * GeometricTail);
        }
    }

    /// <summary>Computes the central binomial coefficient <c>C(2k,k)</c>.</summary>
    /// <param name="k">The index, at least one.</param>
    /// <returns><c>C(2k,k)</c>.</returns>
    /// <remarks>
    /// By the product <c>C(2k,k) = product over i = 1..k of (k+i)/i</c>, whose partial products
    /// are the integers <c>C(k+j, j)</c>, so every division is exact and no intermediate leaves
    /// the integers. Deliberately a different route from the recurrence
    /// <see cref="Refinements"/> advances, so the two agreeing is evidence rather than
    /// tautology.
    /// </remarks>
    private static BigInteger CentralBinomial(long k)
    {
        BigInteger result = BigInteger.One;

        for (long i = 1; i <= k; i++)
        {
            result = result * (k + i) / i;
        }

        return result;
    }
}
