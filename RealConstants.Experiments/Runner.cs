using System.Diagnostics;
using System.Numerics;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>Why a walk over a provider's refinements stopped.</summary>
internal enum StopReason
{
    /// <summary>The requested error target was met. The only outcome that is not a stop rule.</summary>
    TargetMet,

    /// <summary>The step ceiling was reached.</summary>
    StepLimit,

    /// <summary>The wall-clock ceiling was reached.</summary>
    TimeLimit,

    /// <summary>The denominator bit-length ceiling was reached.</summary>
    SizeLimit,

    /// <summary>The user asked to stop. Only reachable from the interactive walk.</summary>
    UserQuit,

    /// <summary>
    /// The bound became finer than the oracle, so nothing further can be measured.
    /// </summary>
    /// <remarks>
    /// Only reachable from the interactive walk. <see cref="MethodComparison"/> walks to fixed
    /// targets that its oracle depths are chosen to stay well ahead of, and a cell there that did
    /// go past reports it in the realised columns without ending the row.
    /// </remarks>
    OracleLimit,
}

/// <summary>The stop rules every walk is subject to, and the outcome of one walk.</summary>
/// <remarks>
/// <para>
/// A run that stops is recorded as <b>what was observed</b> - "did not finish within N steps",
/// "within N seconds", "within N bits" - and never as a verdict about the method.
/// <see cref="DirectSumZeta"/> will hit a rule at any serious target; that is the expected
/// result and the reason it is in the table at all.
/// </para>
/// <para>
/// The size rule is the one that matters most and the one an eye would miss. In exact rational
/// arithmetic a partial sum over <c>k &lt; N</c> carries something like <c>lcm(1..N)</c> as its
/// denominator, which grows exponentially in <c>N</c>: fifty thousand terms is already a
/// denominator of some 144 kilobits. Time and steps are proxies for that; bits is the thing
/// itself.
/// </para>
/// </remarks>
internal sealed record StopRules(int MaxSteps, double MaxSeconds, long MaxDenominatorBits)
{
    /// <summary>The rules every experiment in this project runs under. Change them here.</summary>
    public static StopRules Default { get; } = new(MaxSteps: 200_000, MaxSeconds: 10.0, MaxDenominatorBits: 250_000);
}

/// <summary>What one walk produced.</summary>
/// <param name="Steps">How many refinements were pulled.</param>
/// <param name="Reached">The last enclosure pulled, if any.</param>
/// <param name="Elapsed">Wall-clock time for the walk.</param>
/// <param name="Reason">Why it stopped.</param>
internal sealed record WalkResult(int Steps, Approximation Reached, TimeSpan Elapsed, StopReason Reason)
{
    /// <summary>A phrase naming what happened, for a table cell.</summary>
    /// <param name="rules">The rules the walk ran under.</param>
    /// <returns>The phrase.</returns>
    public string Describe(StopRules rules) => Reason switch
    {
        StopReason.TargetMet => "target met",
        StopReason.StepLimit => $"did not finish within {rules.MaxSteps} steps",
        StopReason.TimeLimit => $"did not finish within {rules.MaxSeconds:F0} s",
        StopReason.SizeLimit => $"did not finish within {rules.MaxDenominatorBits} bits",
        StopReason.UserQuit => "stopped by the user",
        StopReason.OracleLimit =>
            "the bound is finer than the oracle, so there is nothing further to read - "
            + "raise this constant's oracle depth in Catalogue.Constants to go deeper",
        _ => "unknown",
    };
}

/// <summary>Walks a provider's refinements under the stop rules.</summary>
internal static class Runner
{
    /// <summary>Pulls refinements until the target is met or a stop rule fires.</summary>
    /// <param name="constant">The provider, held as the interface so no concrete type is in play.</param>
    /// <param name="target">The error target.</param>
    /// <param name="rules">The stop rules.</param>
    /// <returns>What happened.</returns>
    public static WalkResult WalkTo(IRealConstant constant, BigRational target, StopRules rules)
    {
        ArgumentNullException.ThrowIfNull(constant);
        ArgumentNullException.ThrowIfNull(rules);

        Stopwatch clock = Stopwatch.StartNew();
        Approximation reached = default;
        int steps = 0;

        foreach (Approximation refinement in constant.Refinements())
        {
            reached = refinement;
            steps++;

            if (refinement.MaxError <= target)
            {
                return new WalkResult(steps, reached, clock.Elapsed, StopReason.TargetMet);
            }

            if (steps >= rules.MaxSteps)
            {
                return new WalkResult(steps, reached, clock.Elapsed, StopReason.StepLimit);
            }

            if (clock.Elapsed.TotalSeconds >= rules.MaxSeconds)
            {
                return new WalkResult(steps, reached, clock.Elapsed, StopReason.TimeLimit);
            }

            if (DenominatorBits(refinement) >= rules.MaxDenominatorBits)
            {
                return new WalkResult(steps, reached, clock.Elapsed, StopReason.SizeLimit);
            }
        }

        throw new InvalidOperationException("Refinements() ended, which the contract forbids.");
    }

    /// <summary>The bit-length of the larger of the two denominators an enclosure carries.</summary>
    /// <param name="enclosure">The enclosure.</param>
    /// <returns>The bit-length.</returns>
    public static long DenominatorBits(Approximation enclosure) =>
        BigInteger.Max(enclosure.Value.Denominator, enclosure.MaxError.Denominator).GetBitLength();

    /// <summary>Builds ten raised to a negative power, exactly.</summary>
    /// <param name="places">How many decimal places down.</param>
    /// <returns><c>10^-places</c>.</returns>
    public static BigRational TenToTheMinus(int places) =>
        new(BigInteger.One, BigInteger.Pow(10, places));
}
