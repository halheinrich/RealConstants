using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// The square root of a non-square integer, by Newton's method on <c>f(x) = x^2 - c</c>
/// approached from above.
/// </summary>
/// <remarks>
/// <para>
/// The iteration is <c>x_(n+1) = (x_n + c/x_n)/2</c>, started at <c>x_0 = ceiling(sqrt(c))</c>.
/// Step <c>n</c> is <c>x_n</c>, so step 0 is that starting integer.
/// </para>
/// <para>
/// <b>One type, taking the radicand, rather than a type per root.</b> Two providers differing
/// only in <c>c</c> would be one rule written twice, which the umbrella's writing-code contract
/// forbids: the duplication would encode no independent decision. The independence ruling in
/// <c>SPEC-rational-ratio.md</c> section 4 does not reach this - it forbids two providers
/// <i>of the same constant</i> sharing an engine, and the square root of two and the square root
/// of three are different constants with one provider each.
/// </para>
/// <para>
/// <b>There is no cross-check partner for this provider</b>, and none is implied. Section 4
/// names cross-check pairs for pi and for zeta(3) and names none for square roots. What stands
/// in for one is an oracle rather than a second provider: <c>IntegerMath.Sqrt</c> on a large
/// scaled integer, which the tests use, is an independent computation and is self-verifying by
/// two integer multiplications - but it is not a provider, and agreement with it is not the
/// evidence a provider pair gives.
/// </para>
/// <para>
/// <b>The domain is the non-square integers at or above two.</b> A perfect square is refused
/// rather than answered exactly: its root is rational, so the iteration would sit on the answer
/// from step 0 and every later refinement would repeat it, breaking the strictly-improving
/// obligation of <see cref="IRealConstant.Refinements"/>. A constant that is already rational
/// does not need a provider. Zero and one are refused with them, and the lower limit of two is
/// what the bound's proof below rests on.
/// </para>
/// <para>
/// <b>A rational lower bound on the root, fixed once.</b> Every bound here divides by something
/// no larger than <c>sqrt(c)</c>, and <c>sqrt(c)</c> is exactly what is not available, so the
/// construction needs a rational <c>r</c> with <c>0 &lt; r &lt;= sqrt(c)</c>. It is taken as
/// <c>r = floor(sqrt(c * 2^64)) / 2^32</c>, which is <c>sqrt(c)</c> truncated to 32 fractional
/// bits: at or below <c>sqrt(c)</c> because the floor is at or below <c>sqrt(c) * 2^32</c>, and
/// within <c>2^-32</c> of it because the floor is above <c>sqrt(c) * 2^32 - 1</c>. Both facts
/// are used. Only <c>r &gt;= sqrt(c) - 1/4</c> is needed for the proof; 32 bits is far more than
/// that and makes the loss from using <c>r</c> in place of <c>sqrt(c)</c> invisible in the
/// bound, while keeping <c>r</c> small enough that powers of it stay cheap.
/// </para>
/// <para>
/// <b>The iterates stay above the root, and descend.</b> Completing the square gives
/// <c>x_(n+1) - sqrt(c) = (x_n - sqrt(c))^2 / (2*x_n)</c>, which is at or above zero for any
/// positive <c>x_n</c>. Since <c>x_0 = ceiling(sqrt(c)) &gt; sqrt(c) &gt; 0</c>, induction puts
/// every iterate at or above <c>sqrt(c)</c>, and all of them are positive. Writing
/// <c>e_n = x_n - sqrt(c) &gt;= 0</c>, the same identity reads
/// <c>e_(n+1) = e_n^2 / (2*x_n)</c>. The descent is
/// <c>x_n - x_(n+1) = (x_n^2 - c) / (2*x_n)</c>, strictly positive because <c>sqrt(c)</c> is
/// irrational and so <c>x_n^2 &gt; c</c> at every step.
/// </para>
/// <para>
/// <b>The realised bound, carried by each refinement.</b> Factoring the difference of squares,
/// <c>e_n = (x_n^2 - c) / (x_n + sqrt(c))</c>, and replacing <c>sqrt(c)</c> by the smaller
/// <c>r</c> in the denominator only enlarges the quotient:
/// </para>
/// <para>
/// <c>e_n &lt;= (x_n^2 - c) / (x_n + r) =: A_n</c>.
/// </para>
/// <para>
/// Everything in <c>A_n</c> is known once <c>x_n</c> is, so it costs no part of step
/// <c>n+1</c>'s work. It is strictly decreasing: the map <c>t to (t^2 - c)/(t + r)</c> is
/// strictly increasing above <c>sqrt(c)</c>, since for <c>sqrt(c) &lt;= v &lt; u</c> the
/// difference of its values has numerator <c>(u - v) * (u*v + r*(u + v) + c) &gt; 0</c>, and the
/// iterates strictly decrease.
/// </para>
/// <para>
/// <b>The planned bound, returned by <see cref="ErrorBoundAt"/>.</b> This one must be a closed
/// form in the step index, computable without running the iteration, which is what makes a run
/// plannable before it is paid for. Since <c>x_n &gt;= sqrt(c) &gt;= r &gt; 0</c>, the recursion
/// above weakens to <c>e_(n+1) &lt;= e_n^2 / (2*r)</c>, and
/// <c>e_0 = x_0 - sqrt(c) &lt;= x_0 - r</c>. Define <c>E_0 = x_0 - r</c> and
/// <c>E_(n+1) = E_n^2 / (2*r)</c>. Squaring preserves order on non-negative numbers, so
/// <c>e_n &lt;= E_n</c> at every step by induction. Setting
/// <c>W = E_0 / (2*r) = (x_0 - r) / (2*r)</c> unrolls that recursion into
/// </para>
/// <para>
/// <c>E_n = 2*r * W^(2^n)</c>,
/// </para>
/// <para>
/// which is what this type returns. Check it at <c>n = 0</c>, where it is
/// <c>2*r*W = x_0 - r</c>, and across the step, where
/// <c>2*r * (W^(2^n))^2 = (2*r*W^(2^n))^2 / (2*r) = E_n^2/(2*r)</c>. The doubling exponent is
/// the quadratic convergence made explicit: each step roughly squares the accuracy.
/// </para>
/// <para>
/// <b>Why <c>W &lt; 1</c>, which is what makes that sequence tend to zero.</b> The construction
/// gives <c>r &gt;= sqrt(c) - 1/4</c>, and <c>x_0 = ceiling(sqrt(c)) &lt; sqrt(c) + 1</c>
/// because <c>c</c> is not a square. So the numerator <c>x_0 - r &lt; 5/4</c>, while the
/// denominator <c>2*r &gt;= 2*sqrt(c) - 1/2 &gt;= 2*sqrt(2) - 1/2 &gt; 2.3</c> since
/// <c>c &gt;= 2</c>. Hence <c>W &lt; (5/4)/2.3 &lt; 1</c> for every radicand this type accepts,
/// with no appeal to the particular one. <c>W &gt; 0</c> because
/// <c>r &lt;= sqrt(c) &lt; x_0</c>. The bound is therefore strictly decreasing and tends to
/// zero.
/// </para>
/// <para>
/// <b>The realised bound never exceeds the planned one</b>, so a step chosen from
/// <see cref="ErrorBoundAt"/> delivers at least what it promised. Write
/// <c>L_n = (x_n + sqrt(c)) / (x_n + r) &gt;= 1</c>, so that <c>A_n = L_n * e_n</c>. At step 0,
/// <c>A_0 &lt;= E_0</c> reduces to <c>x_0^2 - c &lt;= x_0^2 - r^2</c>, which is
/// <c>r^2 &lt;= c</c>. Across the step, <c>A_(n+1) = L_(n+1) * e_n^2/(2*x_n)</c> while
/// <c>E_(n+1) = E_n^2/(2*r) &gt;= L_n^2 * e_n^2/(2*r)</c>, so it is enough that
/// <c>L_(n+1) * r &lt;= L_n^2 * x_n</c>. And <c>L</c> decreases in its argument, so
/// <c>L_(n+1) &lt;= 2*sqrt(c)/(sqrt(c) + r)</c>, whence
/// <c>L_(n+1) * r &lt;= 2*r*sqrt(c)/(sqrt(c) + r) &lt;= sqrt(c) &lt;= x_n &lt;= L_n^2 * x_n</c>,
/// the middle inequality being <c>r &lt;= sqrt(c)</c> again.
/// </para>
/// <para>
/// <b>What the two bounds cost, and where the planned one loses.</b> <see cref="ErrorBoundAt"/>
/// is <c>step</c> squarings of a rational and one multiplication; the step itself is that many
/// squarings <i>plus</i> a division and an addition of rationals of the same size at every one
/// of them, each renormalised by a greatest common divisor. So the gap the interface asks for is
/// real, though it is a factor rather than an order: no bound on a quantity of magnitude
/// <c>10^(-k*2^n)</c> can be written down in fewer than about <c>2^n</c> bits, whichever route
/// reaches it. The planned bound is also the looser of the two, and looser by a margin that
/// itself grows doubly exponentially: the ratio <c>E_n / e_n</c> obeys
/// <c>E_(n+1)/e_(n+1) = (E_n/e_n)^2 * (x_n / r)</c>, and that trailing factor falls to one as
/// the iterates settle, so the ratio simply squares from then on. For the square root of two it
/// is about 1.4 at step 1 and about 1.7e5 by step 6. Squaring a ratio each time the accuracy
/// squares costs a fixed <i>fraction</i> of the digits rather than a fixed number of them -
/// about a ninth of them for the square root of two. That is the price of a bound that does not
/// look at the iterate, and it is why <c>IRealConstant.ApproximateTo</c> stops on the realised
/// bound instead.
/// </para>
/// <para>
/// <b>Flagged: the defaulted <c>StepFor</c> search is a poor fit for a doubly exponential
/// bound.</b> It brackets by doubling the step, so answering "step <c>n</c>" first evaluates the
/// bound at a step below <c>2n</c> - and here that probe costs about the square of what the
/// answer costs, and can exceed <see cref="ErrorBoundAt"/>'s guard while the answer itself sits
/// well inside it. A linear scan from zero would be strictly better for this provider, since the
/// answer is never far from zero. It is not reimplemented here: the interface states that a type
/// wanting these members on its own surface should delegate rather than reimplement, and
/// diverging from a ratified contract to tune one implementation is a design change rather than
/// an implementation choice. Targets down to roughly <c>10^-44000</c> are unaffected.
/// </para>
/// <para>
/// Instances carry no mutable state, so one may be shared freely and every member is
/// thread-safe. Each call to <see cref="Refinements"/> returns a fresh, independent sequence.
/// </para>
/// </remarks>
public sealed class NewtonSquareRoot : IRealConstant
{
    /// <summary>
    /// How many fractional bits of <c>sqrt(c)</c> the rational lower bound keeps. The proof
    /// needs only two; the surplus is spent on making the bound's dependence on <c>r</c>
    /// negligible without making powers of <c>r</c> expensive.
    /// </summary>
    private const int LowerBoundFractionBits = 32;

    /// <summary>
    /// The largest step whose bound this type will compute. The closed form raises <c>W</c> to
    /// <c>2^step</c>, and <c>2^31</c> does not fit the <see cref="int"/> exponent
    /// <see cref="BigRational.Pow(BigRational, int)"/> takes.
    /// </summary>
    private const int MaxBoundableStep = 30;

    private const string RadicandTooSmallMessage =
        "The radicand must be at least two. Zero and one have rational square roots, and the " +
        "proof of this type's error bound assumes a radicand of at least two.";

    private const string RadicandIsSquareMessage =
        "The radicand is a perfect square, so its square root is rational and needs no " +
        "approximation. Refining it would repeat one exact value forever, which breaks the " +
        "strictly-improving obligation of IRealConstant.Refinements.";

    private const string StepTooLargeMessage =
        "The error bound at this step is not computable: the closed form's exponent 2^step " +
        "exceeds the int range BigRational.Pow accepts. Long before this step the bound is a " +
        "rational too large to hold in memory, because a bound of that magnitude needs about " +
        "2^step bits however it is reached.";

    /// <summary>The radicand as a rational, for the division inside the iteration.</summary>
    private readonly BigRational _radicand;

    /// <summary>The rational lower bound <c>r</c> on the root.</summary>
    private readonly BigRational _lowerBound;

    /// <summary>The starting iterate <c>x_0 = ceiling(sqrt(c))</c>.</summary>
    private readonly BigRational _start;

    /// <summary>Twice the lower bound, <c>2*r</c>: the scaling in front of the closed form.</summary>
    private readonly BigRational _twiceLowerBound;

    /// <summary>The base <c>W</c> of the closed form, strictly between zero and one.</summary>
    private readonly BigRational _contraction;

    /// <summary>Initialises a provider for the square root of the given radicand.</summary>
    /// <param name="radicand">A non-square integer of at least two.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="radicand"/> is below two, or is a perfect square.
    /// </exception>
    /// <remarks>
    /// Everything the bound depends on is computed here, once: the rational lower bound on the
    /// root, the starting iterate, and the contraction base. Two integer square roots and no
    /// iteration.
    /// </remarks>
    public NewtonSquareRoot(BigInteger radicand)
    {
        if (radicand < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(radicand), radicand, RadicandTooSmallMessage);
        }

        BigInteger floor = IntegerMath.Sqrt(radicand, IntegerSqrtRounding.Floor);
        if (floor * floor == radicand)
        {
            throw new ArgumentOutOfRangeException(nameof(radicand), radicand, RadicandIsSquareMessage);
        }

        Radicand = radicand;
        _radicand = radicand;

        // r = floor(sqrt(c * 2^(2b))) / 2^b: sqrt(c) truncated to b fractional bits, so it is at
        // or below sqrt(c) and within 2^-b of it. Both directions are load-bearing.
        _lowerBound = new BigRational(
            IntegerMath.Sqrt(radicand << (2 * LowerBoundFractionBits), IntegerSqrtRounding.Floor),
            BigInteger.One << LowerBoundFractionBits);

        // The radicand is not a square, so the ceiling is one above the floor, strictly above the
        // root, and the contraction base below is strictly positive.
        _start = floor + BigInteger.One;

        _twiceLowerBound = 2 * _lowerBound;
        _contraction = (_start - _lowerBound) / _twiceLowerBound;
    }

    /// <summary>Gets the integer whose square root this provider approximates.</summary>
    public BigInteger Radicand { get; }

    /// <summary>
    /// Gets the proven upper bound on the error of step <paramref name="step"/>, without
    /// computing that step.
    /// </summary>
    /// <param name="step">The zero-based step index, matching the position in <see cref="Refinements"/>.</param>
    /// <returns><c>2*r * W^(2^step)</c>, the closed form derived in this type's remarks.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="step"/> is negative, or is above 30, past which the exponent
    /// <c>2^step</c> does not fit an <see cref="int"/>.
    /// </exception>
    public BigRational ErrorBoundAt(int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);

        if (step > MaxBoundableStep)
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, StepTooLargeMessage);
        }

        return _twiceLowerBound * BigRational.Pow(_contraction, 1 << step);
    }

    /// <summary>Gets the endless sequence of successively better enclosures of the root.</summary>
    /// <returns>
    /// A lazy, endless sequence. Element <c>n</c> holds <c>x_n</c> with the realised bound
    /// <c>(x_n^2 - c) / (x_n + r)</c>, which is at or below <see cref="ErrorBoundAt"/> for that
    /// step and usually far below it.
    /// </returns>
    /// <remarks>
    /// Incremental by construction: each element is one Newton step from the last. The bound is
    /// read off the iterate the element already carries, so it costs no part of the next step -
    /// which is the whole reason the residual <c>x_n^2 - c</c> is the handle used, rather than
    /// the gap to the following iterate.
    /// </remarks>
    public IEnumerable<Approximation> Refinements()
    {
        BigRational x = _start;

        while (true)
        {
            // Positive at every step, since the radicand is not a square and the iterates stay
            // strictly above the root.
            BigRational residual = (x * x) - _radicand;

            yield return Approximation.Create(x, residual / (x + _lowerBound));

            x = (x + (_radicand / x)) / 2;
        }
    }
}
