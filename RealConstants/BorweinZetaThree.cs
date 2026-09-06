using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// Apery's constant, zeta(3), by Borwein's Chebyshev acceleration of the alternating zeta
/// function.
/// </summary>
/// <remarks>
/// <para>
/// Step <c>n</c> uses the Chebyshev polynomial of degree <c>m = n+1</c> and consumes the
/// reciprocal cubes <c>1/1^3 .. 1/(m)^3</c>, so step 0 is a single term and the value
/// <c>8/9</c>. This is not a truncated series and its bound is not an alternating-series
/// remainder; it is a weighted recombination of the first <c>m</c> terms whose weights are
/// chosen to kill the tail, and the bound comes from how well a polynomial can be small on
/// <c>[0,1]</c> while being large at <c>-1</c>.
/// </para>
/// <para>
/// <b>The derivation, which is also the proof of the bound.</b> Each step is elementary and can
/// be checked by hand; nothing below is cited.
/// </para>
/// <para>
/// <b>1. A positive measure.</b> Substituting <c>x = e^-t</c> in the gamma integral gives
/// <c>integral over [0,1] of x^k * (ln(1/x))^2 / 2 dx = 1/(k+1)^3</c>. Write
/// <c>dmu(x) = (ln(1/x))^2 / 2 dx</c>, which is non-negative on <c>(0,1)</c>. That
/// non-negativity is the whole reason a bound exists at all, and it is used twice below.
/// </para>
/// <para>
/// <b>2. The alternating zeta as one integral.</b> Summing the geometric series inside the
/// integral, <c>eta(3) = sum over k &gt;= 0 of (-1)^k/(k+1)^3 = integral of dmu(x)/(1+x)</c>.
/// </para>
/// <para>
/// <b>3. Any polynomial splits it.</b> Let <c>P</c> have degree <c>m</c> with
/// <c>P(-1) != 0</c>. Then <c>P(-1) - P(x)</c> vanishes at <c>x = -1</c>, so
/// <c>P(-1) - P(x) = (1+x)*R(x)</c> for a polynomial <c>R</c> of degree <c>m-1</c>. Adding and
/// subtracting <c>P(x)</c> in the numerator gives the identity
/// </para>
/// <para>
/// <c>1/(1+x) = R(x)/P(-1) + P(x)/(P(-1)*(1+x))</c>.
/// </para>
/// <para>
/// <b>4. Integrate it.</b> The first piece is a finite combination of the moments already named:
/// with <c>R(x) = sum of r_k x^k</c>, it integrates to
/// <c>(1/P(-1)) * sum over k &lt; m of r_k/(k+1)^3</c>, which is the approximation this type
/// returns. The second piece is the error,
/// <c>gamma_m = (1/P(-1)) * integral of P(x) dmu(x)/(1+x)</c>.
/// </para>
/// <para>
/// <b>5. Bound the error.</b> Since <c>dmu/(1+x)</c> is non-negative on <c>[0,1]</c>, pulling
/// the maximum of <c>|P|</c> out of the integral leaves exactly the integral of step 2:
/// </para>
/// <para>
/// <c>|gamma_m| &lt;= eta(3) * max over [0,1] of |P| / |P(-1)|</c>.
/// </para>
/// <para>
/// <b>6. Choose the polynomial.</b> That bound asks for a polynomial that is small on
/// <c>[0,1]</c> and large at <c>-1</c>, which is precisely the extremal problem the Chebyshev
/// polynomials solve. Take <c>P(x) = T_m(2x-1)</c>. On <c>[0,1]</c> the argument
/// <c>2x-1</c> covers <c>[-1,1]</c>, where <c>|T_m| &lt;= 1</c>, so the numerator is
/// <c>1</c>. At <c>x = -1</c> the argument is <c>-3</c>, and
/// <c>|T_m(-3)| = T_m(3)</c>, an <b>integer</b> given by <c>T_0 = 1</c>, <c>T_1 = 3</c>,
/// <c>T_(m+1) = 6*T_m - T_(m-1)</c> - the values 3, 17, 99, 577, 3363, ... - which grows like
/// <c>(3 + sqrt 8)^m</c>.
/// </para>
/// <para>
/// <b>7. Make it computable.</b> <c>eta(3)</c> is itself an alternating series with strictly
/// decreasing terms starting at 1, so <c>eta(3) &lt; 1</c> with no further work. And
/// <c>zeta(3) = eta(3)/(1 - 2^(1-3)) = (4/3)*eta(3)</c>. Both scalings therefore land on the
/// bound as well as on the value, giving
/// </para>
/// <para>
/// <c>|zeta(3) - value| &lt;= (4/3) / T_m(3)</c>,
/// </para>
/// <para>
/// which is what <see cref="ErrorBoundAt"/> returns. Roughly <c>0.77</c> decimal digits per
/// step, since <c>log10(3 + sqrt 8)</c> is about <c>0.766</c>.
/// </para>
/// <para>
/// <b>The bound is honest but not tight, and the gap widens.</b> Step 5 replaces an oscillating
/// integral by the maximum of <c>|P|</c>, discarding the cancellation that is most of why the
/// scheme works. Measured against an independent value, the realised error is about
/// <c>0.70</c> of the claimed bound at step 0 and about <c>0.009</c> of it by step 39. That is
/// a permitted direction - a bound may be loose, never short - but it has a consequence the
/// tests must respect: halving this bound is refuted only at step 0, where it is nearly tight,
/// and asserting that a halved bound fails at deeper steps would be asserting something false.
/// </para>
/// <para>
/// <b>Two mistakes are available in the scalings and they behave differently.</b> Dropping the
/// <c>4/3</c> from the <i>value</i> reports <c>eta(3)</c> in place of <c>zeta(3)</c> and fails
/// at once, by about a quarter of the answer. Dropping it from the <i>bound</i> alone leaves
/// <c>1/T_m(3)</c>, which is three quarters of the correct bound and - measured - still holds
/// at every step tested, because the slack above is larger than the factor. That is exactly why
/// it stays: a bound that survives because the quantity it dropped happened to be covered by
/// slack is measured rather than proven, and this project forbids measured bounds.
/// </para>
/// <para>
/// <b>What is and is not incremental.</b> The contract asks each refinement to build on the
/// last. The Chebyshev polynomial does: it advances by
/// <c>P_(m+1) = (4x-2)*P_m - P_(m-1)</c>, carrying two coefficient vectors, and the reciprocal
/// cubes are accumulated once each and reused. The recombination cannot: the weights
/// <c>r_k</c> depend on the degree, so the division by <c>1+x</c> and the weighted sum are
/// redone at every step, which makes reaching step <c>n</c> quadratic in <c>n</c> where
/// <see cref="AperyZetaThree"/> is linear. That is a property of the scheme rather than of this
/// implementation - an acceleration whose weights did not depend on the depth would not
/// accelerate - and it is stated here rather than left for a reader to discover from a profile.
/// </para>
/// <para>
/// <b>No step guard.</b> The step index enters only as the degree <c>m = step + 1</c>, carried
/// in a <see cref="long"/>, so no <see cref="int"/> wraps and there is no index at which this
/// type must refuse. An absurd step exhausts memory rather than returning a wrong bound. See
/// halheinrich/Math#53 for why an avoidable guard is worth avoiding.
/// </para>
/// <para>
/// This provider shares no code with <see cref="AperyZetaThree"/>. The two exist to check each
/// other, and a defect in a shared engine would move both values the same way, leaving them in
/// agreement inside their bounds at exactly the moment the check was needed.
/// <c>SPEC-rational-ratio.md</c> section 4 ratifies that as a rule rather than a preference.
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
public sealed class BorweinZetaThree : IRealConstant
{
    /// <summary>
    /// The <c>4/3</c> that converts <c>eta(3)</c> to <c>zeta(3)</c>, from
    /// <c>1 - 2^(1-3) = 3/4</c>. Stated once, so the scaling cannot be applied to the value and
    /// forgotten on the bound.
    /// </summary>
    private static readonly BigRational EtaToZetaThree = new(4, 3);

    /// <summary>
    /// Gets the proven upper bound on the error of step <paramref name="step"/>, without
    /// computing that step.
    /// </summary>
    /// <param name="step">The zero-based step index, matching the position in <see cref="Refinements"/>.</param>
    /// <returns><c>(4/3) / T_m(3)</c> where <c>m = step + 1</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="step"/> is negative.</exception>
    /// <remarks>
    /// <c>step</c> additions and doublings of an integer that grows by about two and a half bits
    /// a step, where the step itself builds a polynomial of that degree with coefficients of
    /// comparable size, divides it, and forms a weighted sum of rationals. The gap the interface
    /// asks for is a wide one here.
    /// </remarks>
    public BigRational ErrorBoundAt(int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);

        // Carried in long. At step = int.MaxValue the degree would wrap in int, the recurrence
        // below would not run, and the bound would come back as 4/3 - larger than step 0's,
        // breaking the non-increasing obligation at the step a bracketing StepFor trusts most.
        long degree = (long)step + 1;

        return EtaToZetaThree * new BigRational(BigInteger.One, ChebyshevAtThree(degree));
    }

    /// <summary>Gets the endless sequence of successively better enclosures of zeta(3).</summary>
    /// <returns>
    /// A lazy, endless sequence. Element <c>n</c> holds the degree-<c>(n+1)</c> Chebyshev
    /// recombination of the reciprocal cubes, with the <see cref="ErrorBoundAt"/> bound for that
    /// step.
    /// </returns>
    /// <remarks>
    /// The bound each element carries is <c>(4/3)</c> over the polynomial evaluated at
    /// <c>-1</c>, taken in magnitude. <see cref="ErrorBoundAt"/> reaches the same number by the
    /// integer recurrence <c>T_(m+1) = 6*T_m - T_(m-1)</c> instead, so the two agreeing is
    /// evidence about the identity <c>|P_m(-1)| = T_m(3)</c> rather than a tautology.
    /// </remarks>
    public IEnumerable<Approximation> Refinements()
    {
        // P_0 = 1 and P_1 = 2x - 1, ascending coefficients. The sequence starts at degree 1.
        List<BigInteger> previous = [BigInteger.One];
        List<BigInteger> current = [BigInteger.MinusOne, 2];

        // 1/(k+1)^3 for k = 0, 1, 2, ..., grown one entry per step and never recomputed.
        List<BigRational> reciprocalCubes = [BigRational.One];

        while (true)
        {
            int degree = current.Count - 1;
            BigInteger atMinusOne = EvaluateAtMinusOne(current);

            // R(x) = (P(-1) - P(x)) / (1 + x), by synthetic division. Writing
            // D(x) = P(-1) - P(x), the coefficients of R come out of the top down:
            // r_(m-1) = d_m, then r_(i-1) = d_i - r_i. Only d_i for i >= 1 is needed, and
            // there d_i is just -p_i; d_0 closes the division and is not used.
            BigInteger[] weights = new BigInteger[degree];
            BigInteger carried = BigInteger.Zero;

            for (int i = degree; i >= 1; i--)
            {
                weights[i - 1] = -current[i] + carried;
                carried = -weights[i - 1];
            }

            BigRational eta = BigRational.Zero;
            for (int k = 0; k < degree; k++)
            {
                eta += weights[k] * reciprocalCubes[k];
            }

            eta /= atMinusOne;

            yield return Approximation.Create(
                EtaToZetaThree * eta,
                EtaToZetaThree * new BigRational(BigInteger.One, BigInteger.Abs(atMinusOne)));

            (previous, current) = (current, Advance(current, previous));
            reciprocalCubes.Add(ReciprocalCube(reciprocalCubes.Count + 1));
        }
    }

    /// <summary>Computes <c>T_m(3)</c>, the Chebyshev polynomial of the first kind at three.</summary>
    /// <param name="degree">The degree <c>m</c>, at least one.</param>
    /// <returns><c>T_m(3)</c>, a positive integer.</returns>
    /// <remarks>
    /// From <c>T_(m+1)(y) = 2*y*T_m(y) - T_(m-1)(y)</c> at <c>y = 3</c>. Every value is an
    /// integer, so no rational arithmetic enters the bound at all.
    /// </remarks>
    private static BigInteger ChebyshevAtThree(long degree)
    {
        BigInteger previous = BigInteger.One;
        BigInteger current = 3;

        for (long i = 1; i < degree; i++)
        {
            (previous, current) = (current, (6 * current) - previous);
        }

        return current;
    }

    /// <summary>Evaluates a polynomial, given by ascending coefficients, at minus one.</summary>
    /// <param name="coefficients">The ascending coefficients.</param>
    /// <returns>The value at <c>-1</c>.</returns>
    private static BigInteger EvaluateAtMinusOne(List<BigInteger> coefficients)
    {
        BigInteger total = BigInteger.Zero;

        for (int i = 0; i < coefficients.Count; i++)
        {
            total = (i % 2 == 0) ? total + coefficients[i] : total - coefficients[i];
        }

        return total;
    }

    /// <summary>Advances the shifted Chebyshev recurrence one degree.</summary>
    /// <param name="current">Coefficients of <c>P_m</c>, ascending.</param>
    /// <param name="previous">Coefficients of <c>P_(m-1)</c>, ascending.</param>
    /// <returns>Coefficients of <c>P_(m+1) = (4x - 2)*P_m - P_(m-1)</c>, ascending.</returns>
    private static List<BigInteger> Advance(List<BigInteger> current, List<BigInteger> previous)
    {
        List<BigInteger> next = new(current.Count + 1);
        for (int i = 0; i <= current.Count; i++)
        {
            next.Add(BigInteger.Zero);
        }

        for (int i = 0; i < current.Count; i++)
        {
            next[i + 1] += 4 * current[i];
            next[i] -= 2 * current[i];
        }

        for (int i = 0; i < previous.Count; i++)
        {
            next[i] -= previous[i];
        }

        return next;
    }

    /// <summary>Builds <c>1/n^3</c> exactly.</summary>
    /// <param name="n">The base, at least one.</param>
    /// <returns>The reciprocal cube.</returns>
    private static BigRational ReciprocalCube(int n)
    {
        BigInteger big = n;
        return new BigRational(BigInteger.One, big * big * big);
    }
}
