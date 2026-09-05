using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// Pi by Machin's formula, <c>pi/4 = 4*arctan(1/5) - arctan(1/239)</c>, with each arctangent
/// taken from its Maclaurin series.
/// </summary>
/// <remarks>
/// <para>
/// <c>arctan(x) = x - x^3/3 + x^5/5 - ...</c>, the sum over <c>k &gt;= 0</c> of
/// <c>(-1)^k * x^(2k+1) / (2k+1)</c>. Step <c>n</c> takes the terms <c>k = 0 .. n</c> of
/// <b>both</b> series and combines them, so the two are always carried to the same depth.
/// </para>
/// <para>
/// This is the workhorse of the pair. Where <see cref="LeibnizPi"/> buys a decimal digit for a
/// tenfold increase in steps, this buys roughly <c>1.4</c> digits per step, so it passes thirty
/// digits in about twenty steps. The two providers share no code, which is what makes their
/// agreement inside their bounds evidence about both rather than about one shared routine.
/// </para>
/// <para>
/// <b>The bound, and why it holds.</b> Each series is alternating in the strict sense: for
/// <c>0 &lt; x &lt; 1</c> the magnitudes <c>x^(2k+1)/(2k+1)</c> are positive, strictly
/// decreasing and tending to zero, since both factors move that way. So each remainder is
/// bounded by that series' own first omitted term - the grouping argument is spelled out in
/// <see cref="LeibnizPi"/> and is the same one here. After step <c>n</c>, writing
/// <c>m = 2n+3</c> for the index of the first omitted power:
/// </para>
/// <para>
/// <c>|arctan(1/5) - A_n| &lt;= (1/5)^m / m</c>, and
/// <c>|arctan(1/239) - B_n| &lt;= (1/239)^m / m</c>.
/// </para>
/// <para>
/// <b>Both series and both scalings enter the bound.</b> The value at step <c>n</c> is
/// <c>4 * (4*A_n - B_n)</c>, so its error is
/// <c>4 * (4*(arctan(1/5) - A_n) - (arctan(1/239) - B_n))</c>, and the triangle inequality
/// gives
/// </para>
/// <para>
/// <c>|pi - value| &lt;= 4 * (4 * (1/5)^m / m + 1 * (1/239)^m / m)</c>,
/// </para>
/// <para>
/// which is what <see cref="ErrorBoundAt"/> returns: <c>16/(5^m * m) + 4/(239^m * m)</c>. Two
/// mistakes are available here and neither announces itself. Bounding only the <c>1/5</c>
/// series drops a real contribution; that term is some seven orders of magnitude smaller, but a
/// bound that holds because a neglected quantity happened to be small is measured rather than
/// proven, and measured bounds are what this project forbids. Applying the coefficient
/// <c>4</c> and forgetting that the identity yields <c>pi/4</c> rather than <c>pi</c>
/// understates the bound fourfold, which fails at every step including the first.
/// </para>
/// <para>
/// The absolute values in the triangle inequality are why the two remainders are added rather
/// than differenced. Their signs do in fact agree, which would license a slightly sharper
/// bound, but that argument leans on both series being carried to the same depth and on the two
/// sign patterns staying in phase. Adding needs no such premise and costs a few parts per
/// million of the bound.
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
public sealed class MachinPi : IRealConstant
{
    /// <summary>The reciprocal of the first argument: the series is <c>arctan(1/5)</c>.</summary>
    private const int FirstReciprocal = 5;

    /// <summary>The reciprocal of the second argument: the series is <c>arctan(1/239)</c>.</summary>
    private const int SecondReciprocal = 239;

    /// <summary>The coefficient on <c>arctan(1/5)</c> in the identity.</summary>
    private const int FirstCoefficient = 4;

    /// <summary>The coefficient on <c>arctan(1/239)</c> in the identity.</summary>
    private const int SecondCoefficient = -1;

    /// <summary>
    /// The four in front of the identity. Machin's formula gives <c>pi/4</c>, so the value and
    /// the error bound are both scaled by it. Stated once, so the scaling cannot be applied to
    /// the value and forgotten on the bound.
    /// </summary>
    private const int IdentityToPi = 4;

    /// <summary>
    /// The largest step whose bound this type will compute. Past it the exponent
    /// <c>2*step+3</c> no longer fits the <see cref="int"/> that <see cref="BigInteger.Pow"/>
    /// takes.
    /// </summary>
    private const int MaxBoundableStep = (int.MaxValue - 3) / 2;

    private const string StepTooLargeMessage =
        "The error bound at this step is not computable: the exponent 2*step+3 exceeds the int " +
        "range BigInteger.Pow accepts. No target error a caller can construct requires a step " +
        "anywhere near this one, because the bound falls by a factor of at least 25 per step.";

    /// <summary>
    /// Gets the proven upper bound on the error of step <paramref name="step"/>, without
    /// computing that step.
    /// </summary>
    /// <param name="step">The zero-based step index, matching the position in <see cref="Refinements"/>.</param>
    /// <returns>
    /// <c>16/(5^m * m) + 4/(239^m * m)</c> where <c>m = 2*step+3</c>: both series' first omitted
    /// terms, each under its own coefficient, all scaled from <c>pi/4</c> to <c>pi</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="step"/> is negative, or is large enough that <c>2*step+3</c> overflows
    /// <see cref="int"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Two exponentiations and a division, where the step itself is a running sum of
    /// <c>step + 1</c> terms in each of two series. That gap is what lets a run be planned
    /// before it is paid for.
    /// </para>
    /// <para>
    /// The upper guard cannot be reached through <c>StepFor</c> by any target a caller can
    /// actually build. The bound falls by a factor of at least 25 per step, so a target small
    /// enough to force a step near <see cref="MaxBoundableStep"/> would itself be a rational
    /// with something like a billion digits. The guard is here so that the failure, if it ever
    /// does arrive, says what happened rather than surfacing from inside
    /// <see cref="BigInteger.Pow"/>.
    /// </para>
    /// </remarks>
    public BigRational ErrorBoundAt(int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);

        if (step > MaxBoundableStep)
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, StepTooLargeMessage);
        }

        // The index of the first omitted power, the same in both series.
        int firstOmitted = (2 * step) + 3;
        BigInteger divisor = firstOmitted;

        BigRational fromFirst = new(
            IdentityToPi * int.Abs(FirstCoefficient),
            BigInteger.Pow(FirstReciprocal, firstOmitted) * divisor);

        BigRational fromSecond = new(
            IdentityToPi * int.Abs(SecondCoefficient),
            BigInteger.Pow(SecondReciprocal, firstOmitted) * divisor);

        // Added, not differenced. The triangle inequality is what makes this a bound rather
        // than an estimate, and it has no access to the two remainders' signs.
        return fromFirst + fromSecond;
    }

    /// <summary>Gets the endless sequence of successively better enclosures of pi.</summary>
    /// <returns>
    /// A lazy, endless sequence. Element <c>n</c> combines both arctangent series over
    /// <c>k = 0 .. n</c>, carrying the <see cref="ErrorBoundAt"/> bound for that step.
    /// </returns>
    /// <remarks>
    /// Incremental, and deliberately free of <see cref="BigInteger.Pow"/>. Each odd power is the
    /// previous one times <c>x^2</c>, so advancing both powers past the terms just consumed
    /// leaves exactly the two first-omitted magnitudes the bound is built from. That is the same
    /// closed form <see cref="ErrorBoundAt"/> computes, reached without repeating the
    /// exponentiation and without a step counter that could overflow.
    /// </remarks>
    public IEnumerable<Approximation> Refinements()
    {
        BigRational firstSum = BigRational.Zero;
        BigRational secondSum = BigRational.Zero;

        // x^(2k+1) for each series, starting at k = 0.
        BigRational firstPower = new(BigInteger.One, FirstReciprocal);
        BigRational secondPower = new(BigInteger.One, SecondReciprocal);

        // The x^2 that each power is advanced by.
        BigRational firstSquare = new(BigInteger.One, FirstReciprocal * FirstReciprocal);
        BigRational secondSquare = new(BigInteger.One, SecondReciprocal * SecondReciprocal);

        // The current term's divisor, 2k+1, and whether that term is added or subtracted.
        BigInteger odd = BigInteger.One;
        bool add = true;

        while (true)
        {
            BigRational reciprocalOdd = new(BigInteger.One, odd);
            BigRational firstTerm = firstPower * reciprocalOdd;
            BigRational secondTerm = secondPower * reciprocalOdd;

            firstSum = add ? firstSum + firstTerm : firstSum - firstTerm;
            secondSum = add ? secondSum + secondTerm : secondSum - secondTerm;
            add = !add;

            // Advance past the terms just consumed. The powers then hold x^(2n+3) and the odd
            // is 2n+3, which is precisely the first omitted term of each series.
            firstPower *= firstSquare;
            secondPower *= secondSquare;
            odd += 2;

            BigRational value = IdentityToPi *
                ((FirstCoefficient * firstSum) + (SecondCoefficient * secondSum));

            BigRational bound = IdentityToPi *
                ((int.Abs(FirstCoefficient) * firstPower) +
                 (int.Abs(SecondCoefficient) * secondPower)) *
                new BigRational(BigInteger.One, odd);

            yield return Approximation.Create(value, bound);
        }
    }
}
