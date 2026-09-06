using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// Apery's constant, zeta(3), by the central-binomial series
/// <c>zeta(3) = (5/2) * sum over k &gt;= 1 of (-1)^(k-1) / (k^3 * C(2k,k))</c>.
/// </summary>
/// <remarks>
/// <para>
/// Step <c>n</c> is the partial sum over <c>k = 1 .. n+1</c>, so step 0 is the single term
/// <c>1/2</c> and the value <c>5/4</c>. The identity is classical - Hjortnaes 1953, and the
/// series Apery used in 1978 to prove zeta(3) irrational - and is <b>cited, not proved here</b>.
/// What is proved here is the error bound, which is the thing this project forbids taking on
/// trust. The identity itself is checked the way any external claim is: the tests enclose an
/// independently computed zeta(3), and the cross-check against <see cref="BorweinZetaThree"/>
/// would refute it if it were wrong.
/// </para>
/// <para>
/// <b>The bound, and why it holds.</b> Write <c>a_k = 1/(k^3 * C(2k,k))</c>. Every <c>a_k</c> is
/// positive. They strictly decrease, and the ratio makes that plain:
/// </para>
/// <para>
/// <c>a_(k+1)/a_k = k^3 / (2 * (k+1)^2 * (2k+1))</c>,
/// </para>
/// <para>
/// because <c>C(2k+2,k+1) = C(2k,k) * 2*(2k+1)/(k+1)</c>. Expanding the denominator gives
/// <c>4k^3 + 10k^2 + 8k + 2</c>, which exceeds <c>4k^3</c> for every <c>k &gt;= 1</c>, so the
/// ratio is strictly below <c>1/4</c>. That settles both obligations at once: the terms strictly
/// decrease, and they tend to zero at least geometrically, since
/// <c>a_k &lt;= a_1 * 4^-(k-1)</c>.
/// </para>
/// <para>
/// So the series is alternating in the strict sense and the standard remainder estimate applies:
/// the distance from a partial sum to the limit is at most the first term left out. The grouping
/// argument for that estimate is written out in full in <see cref="LeibnizPi"/> and is the same
/// one here. Step <c>n</c> leaves out the term at index <c>n+2</c>, so the error in the <i>sum</i>
/// is at most <c>a_(n+2)</c>. Multiplying by the <c>5/2</c> in front - the step that turns a
/// bound on the sum into a bound on zeta(3), and the one that is easy to forget - gives
/// </para>
/// <para>
/// <c>(5/2) / ((n+2)^3 * C(2n+4, n+2))</c>,
/// </para>
/// <para>
/// which is what <see cref="ErrorBoundAt"/> returns. It is a closed form in the step index:
/// derived, never observed, and never read off a run.
/// </para>
/// <para>
/// <b>Roughly 0.6 decimal digits per step</b>, since <c>C(2k,k)</c> grows like <c>4^k</c>. That
/// is slow beside <see cref="MachinPi"/> but it is the same order as
/// <see cref="BorweinZetaThree"/>'s 0.77, which is what a cross-check pair wants: two providers
/// of comparable depth refute each other far more sharply than a deep one paired with a shallow
/// one.
/// </para>
/// <para>
/// <b>No step guard, deliberately.</b> The step index enters only as <c>k = step + 2</c>, which
/// is carried in a <see cref="long"/> and cannot overflow for any <see cref="int"/> step, so
/// there is no index at which this type must refuse. An absurd step is slow rather than wrong -
/// the central binomial coefficient at that index is simply enormous - and that is the honest
/// trade, because a guard is a step at which a bracketing <c>StepFor</c> can throw. See
/// halheinrich/Math#53.
/// </para>
/// <para>
/// This provider shares no code with <see cref="BorweinZetaThree"/>, and that is the design
/// rather than an oversight. The two exist to check each other, and a defect in a shared engine
/// would move both values by the same amount, leaving them in agreement inside their bounds at
/// exactly the moment the check was needed. <c>SPEC-rational-ratio.md</c> section 4 ratifies
/// that as a rule rather than a preference.
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
public sealed class AperyZetaThree : IRealConstant
{
    /// <summary>
    /// The <c>5/2</c> in front of the sum. The series computes <c>(2/5)*zeta(3)</c>, so the
    /// value and the error bound are both scaled by it. Stated once, so the two cannot drift
    /// apart.
    /// </summary>
    private static readonly BigRational IdentityToZetaThree = new(5, 2);

    /// <summary>
    /// Gets the proven upper bound on the error of step <paramref name="step"/>, without
    /// computing that step.
    /// </summary>
    /// <param name="step">The zero-based step index, matching the position in <see cref="Refinements"/>.</param>
    /// <returns>
    /// <c>(5/2) / (k^3 * C(2k,k))</c> with <c>k = step + 2</c>: the first omitted term, scaled
    /// to zeta(3).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="step"/> is negative.</exception>
    /// <remarks>
    /// One central binomial coefficient, where the step itself is <c>step + 1</c> rational
    /// additions on denominators that grow without limit, each renormalised by a greatest common
    /// divisor. The gap is a factor rather than an order - the coefficient is the expensive part
    /// of both - but it is real, and it is what lets a run be planned before it is paid for.
    /// </remarks>
    public BigRational ErrorBoundAt(int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);

        // Carried in long, not int. At step = int.MaxValue the index would wrap in int and the
        // bound would come back larger than step 0's, breaking the non-increasing obligation at
        // exactly the step a bracketing StepFor reaches last and trusts most.
        long firstOmitted = (long)step + 2;
        BigInteger cube = (BigInteger)firstOmitted * firstOmitted * firstOmitted;

        return IdentityToZetaThree * new BigRational(
            BigInteger.One,
            cube * CentralBinomial(firstOmitted));
    }

    /// <summary>Gets the endless sequence of successively better enclosures of zeta(3).</summary>
    /// <returns>
    /// A lazy, endless sequence. Element <c>n</c> holds the partial sum over <c>k = 1 .. n+1</c>,
    /// scaled by <c>5/2</c>, with the <see cref="ErrorBoundAt"/> bound for that step.
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
            BigRational term = new(BigInteger.One, index * index * index * central);
            sum = add ? sum + term : sum - term;
            add = !add;

            // Advance to the next index. C(2k+2,k+1) = C(2k,k) * 2*(2k+1)/(k+1), and the
            // division is exact because its quotient is that binomial coefficient, an integer.
            central = central * 2 * ((2 * index) + 1) / (index + BigInteger.One);
            index += BigInteger.One;

            // index now names the first omitted term, whose magnitude bounds the tail. That
            // agrees with ErrorBoundAt by construction: at step n this index is n+2.
            yield return Approximation.Create(
                IdentityToZetaThree * sum,
                IdentityToZetaThree * new BigRational(
                    BigInteger.One,
                    index * index * index * central));
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
