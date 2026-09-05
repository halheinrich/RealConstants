using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// Pi by the Gregory-Leibniz series: four times the alternating sum of the reciprocals of the
/// odd numbers.
/// </summary>
/// <remarks>
/// <para>
/// <c>pi/4 = 1 - 1/3 + 1/5 - 1/7 + ...</c>, the sum over <c>k &gt;= 0</c> of
/// <c>(-1)^k / (2k+1)</c>. Step <c>n</c> is the partial sum over <c>k = 0 .. n</c>, so step 0 is
/// the single term <c>1</c> and the value <c>4</c>.
/// </para>
/// <para>
/// <b>This is a control, not a workhorse.</b> It converges appallingly slowly - the bound below
/// is <c>4/(2n+3)</c>, so each further decimal digit costs ten times the steps - and that is
/// what it is for. Its product is trust: it is short enough to audit by reading, and it shares
/// no code with <see cref="MachinPi"/>, so the two agreeing inside their bounds is evidence
/// about both rather than evidence about one shared routine. Resist making it faster; the fast
/// provider already exists beside it.
/// </para>
/// <para>
/// <b>The bound, and why it holds.</b> Write <c>a_k = 1/(2k+1)</c>. Those terms are positive,
/// strictly decreasing, and tend to zero, so the series is alternating in the strict sense and
/// the standard remainder estimate applies: group the tail after step <c>n</c> as
/// <c>(a_(n+1) - a_(n+2)) + (a_(n+3) - a_(n+4)) + ...</c>, in which every bracket is positive,
/// and as <c>a_(n+1) - (a_(n+2) - a_(n+3)) - ...</c>, in which every subtracted bracket is
/// positive. The first grouping says the tail's magnitude is at least zero; the second says it
/// is at most <c>a_(n+1)</c>. So the distance from the partial sum to the limit is at most the
/// first term left out.
/// </para>
/// <para>
/// Step <c>n</c> leaves out the term at index <c>n+1</c>, of magnitude <c>1/(2n+3)</c>. That
/// bounds the error in <c>pi/4</c>. Multiplying through by the four in front of the sum - the
/// step that turns a bound on the sum into a bound on pi, and the one that is easy to forget -
/// gives at most <c>4/(2n+3)</c> for pi itself, which is what <see cref="ErrorBoundAt"/>
/// returns. It is a closed form in the step index: derived, never observed, and never read off
/// a run.
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
public sealed class LeibnizPi : IRealConstant
{
    /// <summary>
    /// The four in front of the sum. The series computes <c>pi/4</c>, so the value and the error
    /// bound are both scaled by it. Stated once, so the two cannot drift apart.
    /// </summary>
    private const int IdentityToPi = 4;

    /// <summary>
    /// Gets the proven upper bound on the error of step <paramref name="step"/>, without
    /// computing that step.
    /// </summary>
    /// <param name="step">The zero-based step index, matching the position in <see cref="Refinements"/>.</param>
    /// <returns><c>4/(2*step+3)</c>, the magnitude of the first omitted term scaled to pi.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="step"/> is negative.</exception>
    /// <remarks>
    /// Two integer operations regardless of the step, where the step itself costs
    /// <c>step + 1</c> rational additions on denominators that grow without limit. That gap is
    /// the point of the member: a run can be planned before it is paid for.
    /// </remarks>
    public BigRational ErrorBoundAt(int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);

        // 2*step+3 is computed in long deliberately. In int it overflows for step above
        // (int.MaxValue-3)/2, and at step = int.MaxValue it wraps to 1, so the bound would come
        // back as 4 - larger than every earlier step's, breaking the non-increasing obligation
        // at exactly the step a bracketing StepFor reaches last and trusts most.
        return new BigRational(IdentityToPi, (2L * step) + 3);
    }

    /// <summary>Gets the endless sequence of successively better enclosures of pi.</summary>
    /// <returns>
    /// A lazy, endless sequence. Element <c>n</c> holds the partial sum over <c>k = 0 .. n</c>,
    /// scaled by four, with the <see cref="ErrorBoundAt"/> bound for that step.
    /// </returns>
    /// <remarks>
    /// Incremental: each element adds one term to the previous partial sum and one step to the
    /// running odd number, rather than resumming from the start. The odd number is carried as a
    /// <see cref="BigInteger"/> rather than a step counter, so the sequence is endless in fact
    /// and not merely in intent - an <see cref="int"/> counter would overflow, and the bound it
    /// fed would then be wrong rather than merely absent.
    /// </remarks>
    public IEnumerable<Approximation> Refinements()
    {
        BigRational quarterSum = BigRational.Zero;

        // The current term's denominator, 2k+1, and whether that term is added or subtracted.
        BigInteger odd = BigInteger.One;
        bool add = true;

        while (true)
        {
            BigRational term = new(BigInteger.One, odd);
            quarterSum = add ? quarterSum + term : quarterSum - term;
            add = !add;

            // The first omitted term is 1/(odd+2). Scaled by the four in front of the sum, that
            // is this step's bound, and it agrees with ErrorBoundAt by construction: at step n
            // this odd is 2n+1, so odd+2 is 2n+3.
            odd += 2;

            yield return Approximation.Create(
                IdentityToPi * quarterSum,
                new BigRational(IdentityToPi, odd));
        }
    }
}
