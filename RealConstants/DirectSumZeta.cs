using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// zeta(s) for integer <c>s &gt;= 2</c> straight from its definition: the partial sum of
/// <c>1/k^s</c>, with the tail bracketed between two integrals.
/// </summary>
/// <remarks>
/// <para>
/// Step <c>n</c> sums <c>k = 1 .. n+1</c>. There is no acceleration, no identity and no
/// coefficient: this is the series that <i>defines</i> zeta(s), plus the tightest tail estimate
/// the monotonicity of <c>x^-s</c> gives for free.
/// </para>
/// <para>
/// <b>This is the slow implementation kept deliberately slow.</b> <c>../AGENTS.md</c>
/// section Exactness discipline: "Where a fast implementation sits behind an interface alongside
/// an obviously-correct slow one, the slow one's only product is trust, and it stays trivially
/// auditable." That is this type's entire job. It is <b>not</b> to be optimised, not to be
/// given an Euler transform or a Richardson extrapolation, and not to be quietly replaced by
/// something faster that happens to agree - every one of those trades the only thing it has.
/// If it is ever too slow for a caller, the caller wanted <see cref="EulerMaclaurinZeta"/>.
/// </para>
/// <para>
/// <b>The bound, in two lines.</b> <c>x^-s</c> is strictly decreasing on the positive reals, so
/// for every <c>k &gt;= N+1</c> the term <c>1/k^s</c> is below the integral of <c>x^-s</c> over
/// <c>[k-1, k]</c> and above its integral over <c>[k, k+1]</c>. Summing over all <c>k &gt;
/// N</c> telescopes both sides into whole integrals:
/// </para>
/// <para>
/// <c>(N+1)^(1-s)/(s-1) &lt; tail &lt; N^(1-s)/(s-1)</c>.
/// </para>
/// <para>
/// So zeta(s) lies strictly between the partial sum plus the smaller of those and the partial
/// sum plus the larger. This returns the midpoint of that interval with half its width, which is
/// a valid enclosure and a slightly conservative one, since the true value is strictly inside.
/// Both endpoints come from the same elementary comparison; nothing is cited.
/// </para>
/// <para>
/// <b>The bound falls like <c>N^-s</c> and that is hopeless as a workhorse.</b> Sixty digits of
/// zeta(2) would want something like <c>10^30</c> terms. Worse, in exact rational arithmetic the
/// partial sum carries <c>lcm(1..N)</c> as its denominator, which grows exponentially in
/// <c>N</c>: fifty thousand terms already costs a denominator of some 144 kilobits and a few
/// seconds, to buy four digits. Those are measurements, not estimates, and the experiments
/// project reproduces them. The provider ships anyway, because a bench whose fast methods are
/// checked only against each other has nothing to fall back on when they disagree.
/// </para>
/// <para>
/// <b>The bound is sound but its half-width overstates the realised error, increasingly so.</b>
/// The true tail sits near the middle of the bracket rather than at either end - much nearer
/// than the width admits - so measured realised-over-claimed falls from about 0.42 at step 0 to
/// about 0.0007 by step 1000 for <c>s = 2</c>. Widening a bound is always permitted, but it
/// decides what a falsification test may claim, and the tests say so rather than overstating.
/// The falsification that does bite at every step is dropping the tail estimate altogether: the
/// bare partial sum lies outside its own enclosure, always.
/// </para>
/// <para>
/// <b>No step guard.</b> The step index enters only as <c>N = step + 1</c>, carried in a
/// <see cref="long"/>, so nothing wraps and no index has to be refused. An absurd step is slow
/// rather than wrong - very slow, here, which is the point of the type.
/// </para>
/// <para>
/// This provider shares no code with <see cref="CentralBinomialZeta"/> or
/// <see cref="EulerMaclaurinZeta"/>, and no identity either. It is the third opinion the other
/// two are grounded against.
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
public sealed class DirectSumZeta : IRealConstant
{
    private const string OrderTooSmallMessage =
        "The order must be at least two. At s = 1 the series is the harmonic one and diverges, " +
        "and the tail integrals divide by s - 1.";

    /// <summary>Initialises a provider for zeta of the given order.</summary>
    /// <param name="order">The order <c>s</c>, at least two.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> is below two.</exception>
    public DirectSumZeta(int order)
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
    /// <returns>Half the width of the tail bracket at <c>N = step + 1</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="step"/> is negative.</exception>
    /// <remarks>
    /// Two integer powers and a subtraction, where the step itself sums <c>N</c> rationals whose
    /// common denominator is a least common multiple. Here the gap the interface asks for is an
    /// order rather than a factor, and a wide one: this is the provider whose bound is cheapest
    /// to know and whose value is most expensive to reach.
    /// </remarks>
    public BigRational ErrorBoundAt(int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);

        // Carried in long, so nothing wraps at the top of the int range.
        BigInteger terms = (long)step + 1;

        return TailWidth(Order, terms) / 2;
    }

    /// <summary>Gets the endless sequence of successively better enclosures of zeta(s).</summary>
    /// <returns>
    /// A lazy, endless sequence. Element <c>n</c> holds the partial sum over <c>k = 1 .. n+1</c>
    /// re-centred by the midpoint of the tail bracket, with half that bracket's width as its
    /// bound.
    /// </returns>
    /// <remarks>
    /// Incremental in the partial sum, which gains one reciprocal power a step and is never
    /// resummed. The tail bracket is two integer powers, recomputed because it depends on
    /// <c>N</c> alone.
    /// </remarks>
    public IEnumerable<Approximation> Refinements()
    {
        BigRational partial = BigRational.Zero;
        BigInteger terms = BigInteger.One;

        while (true)
        {
            partial += new BigRational(BigInteger.One, BigInteger.Pow(terms, Order));

            BigRational lower = TailIntegral(Order, terms + BigInteger.One);
            BigRational upper = TailIntegral(Order, terms);

            yield return Approximation.Create(
                partial + ((lower + upper) / 2),
                (upper - lower) / 2);

            terms += BigInteger.One;
        }
    }

    /// <summary>The integral of <c>x^-s</c> from <paramref name="from"/> to infinity.</summary>
    /// <param name="order">The order <c>s</c>.</param>
    /// <param name="from">The lower limit, at least one.</param>
    /// <returns><c>from^(1-s)/(s-1)</c>.</returns>
    private static BigRational TailIntegral(int order, BigInteger from) =>
        new(BigInteger.One, (order - 1) * BigInteger.Pow(from, order - 1));

    /// <summary>The width of the tail bracket after <paramref name="terms"/> terms.</summary>
    /// <param name="order">The order <c>s</c>.</param>
    /// <param name="terms">The number of terms summed exactly.</param>
    /// <returns>The difference between the two tail integrals.</returns>
    private static BigRational TailWidth(int order, BigInteger terms) =>
        TailIntegral(order, terms) - TailIntegral(order, terms + BigInteger.One);
}
