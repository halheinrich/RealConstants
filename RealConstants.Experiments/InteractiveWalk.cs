using System.Diagnostics;
using System.Globalization;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// The interactive walk: one method, one order, one step at a time, so the algorithm can be felt
/// rather than summarised.
/// </summary>
/// <remarks>
/// <para>
/// Every column is derived from <c>ErrorBoundAt</c>, <c>Refinements()</c> and an oracle. No
/// provider was given a diagnostic hook and <c>IRealConstant</c> did not change; putting an
/// experiment's convenience into a ratified contract is the edit to refuse.
/// </para>
/// <para>
/// <b>The prompt is guarded by <see cref="Console.IsInputRedirected"/>.</b> Under a pipe, a
/// redirect or any other non-terminal stdin, there is nobody to press a key, so the walk falls
/// through to the stop rules and prints a line saying the pause was skipped rather than blocking
/// forever on a read that returns end-of-file.
/// </para>
/// </remarks>
internal static class InteractiveWalk
{
    /// <summary>How many decimal places the value column shows.</summary>
    private const int ValuePlaces = 40;

    public static int Walk(string method, int order)
    {
        IRealConstant? constant = Methods.TryCreate(method, order);
        if (constant is null)
        {
            Console.Error.WriteLine($"no method '{method}' with a member at s={order} - try 'list'.");
            return 2;
        }

        StopRules rules = StopRules.Default;
        (Approximation oracle, string oracleDescription) = Methods.Oracle(order, method);

        Console.Error.WriteLine($"stepping {method} at s={order}");
        Console.Error.WriteLine($"  oracle: {oracleDescription}, half-width {Presentation.Magnitude(oracle.MaxError)}");
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  stop rules: {rules.MaxSteps} steps, {rules.MaxSeconds:F0} s, {rules.MaxDenominatorBits} denominator bits"));

        bool interactive = !Console.IsInputRedirected;
        if (interactive)
        {
            Console.Error.WriteLine(
                "  " + string.Join(", ", Keys.Select(k => k.Key + " = " + k.Meaning)));
        }
        else
        {
            Console.Error.WriteLine("  stdin is not a terminal, so the pause is skipped and this runs to a stop rule");
        }

        Console.Error.WriteLine();
        Console.WriteLine("step | value | claimed | realised | realised/claimed | digits | gained | den_bits | ms");

        // Two clocks, because they answer different questions. `computing` is the one the
        // wall-clock stop rule reads, and it is PAUSED while the prompt waits: a rule meant to
        // bound how long a method takes must not count how long a person took to press a key.
        // `session` is the whole elapsed time and is reported alongside it.
        Stopwatch computing = Stopwatch.StartNew();
        Stopwatch session = Stopwatch.StartNew();
        Stopwatch perStep = new();
        int pending = interactive ? 1 : int.MaxValue;
        double previousDigits = 0;
        int step = 0;
        StopReason reason = StopReason.StepLimit;

        // The enumerator is driven by hand rather than by `foreach`, so that `perStep` can
        // bracket MoveNext() and nothing else. Under `foreach` the refinement is computed
        // before the body is entered, so a timer started in the body measures the oracle
        // subtraction and the decimal rendering instead of the step - measured 2026-09-05 on a
        // redirected central s=2 run, where the column summed to 6.80 s of a 10.00 s walk and
        // the 3.20 s it omitted was the stepping the column exists to show.
        using IEnumerator<Approximation> refinements = constant.Refinements().GetEnumerator();

        while (true)
        {
            perStep.Restart();
            if (!refinements.MoveNext())
            {
                throw new InvalidOperationException("Refinements() ended, which the contract forbids.");
            }

            Approximation refinement = refinements.Current;
            TimeSpan stepCost = perStep.Elapsed;

            // The realised error is only known to within the oracle's own half-width, so it is
            // reported as the largest it could be. An oracle far finer than the bound makes that
            // distinction invisible, which is why the oracle is taken deep.
            BigRational realised =
                BigRational.Abs(refinement.Value - oracle.Value) + oracle.MaxError;

            double digits = -Presentation.DecimalExponent(refinement.MaxError);

            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{step} | {Presentation.ToDecimal(refinement.Value, ValuePlaces)} | " +
                $"{Presentation.Magnitude(refinement.MaxError)} | {Presentation.Magnitude(realised)} | " +
                $"{Presentation.Ratio(realised, refinement.MaxError)} | {digits:F1} | " +
                $"{digits - previousDigits:F2} | {Runner.DenominatorBits(refinement)} | " +
                $"{stepCost.TotalMilliseconds:F1}"));

            previousDigits = digits;
            step++;

            if (step >= rules.MaxSteps)
            {
                reason = StopReason.StepLimit;
                break;
            }

            if (computing.Elapsed.TotalSeconds >= rules.MaxSeconds)
            {
                reason = StopReason.TimeLimit;
                break;
            }

            if (Runner.DenominatorBits(refinement) >= rules.MaxDenominatorBits)
            {
                reason = StopReason.SizeLimit;
                break;
            }

            if (!interactive)
            {
                continue;
            }

            pending--;
            if (pending > 0)
            {
                continue;
            }

            // The pause is not the method's cost. Measured 2026-09-05 against the first
            // version of this file, which ran one clock straight through the read and then
            // reported "did not finish within 10 s" after two instant steps and twenty-five
            // seconds of somebody thinking - a false statement about the method, which is the
            // one kind of output this bench may not produce.
            computing.Stop();
            bool stopping = ReadInstruction(rules, ref pending);
            computing.Start();

            if (stopping)
            {
                reason = StopReason.UserQuit;
                break;
            }
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"stopped after {step} steps, {computing.Elapsed.TotalSeconds:F2} s computing " +
            $"({session.Elapsed.TotalSeconds:F2} s including the pause): " +
            $"{new WalkResult(step, default, computing.Elapsed, reason).Describe(rules)}"));

        return 0;
    }

    /// <summary>
    /// Reads one actionable instruction from the prompt, answering help requests first.
    /// </summary>
    /// <param name="rules">The stop rules, which the help screen quotes.</param>
    /// <param name="pending">Set to how many steps to take before prompting again.</param>
    /// <returns><see langword="true"/> if the walker asked to stop.</returns>
    /// <remarks>
    /// A loop rather than a single read, because <c>h</c> must not consume a step: a reader who
    /// has to spend a refinement to find out what the columns mean is being charged for the
    /// question. End of input counts as a request to stop - under a terminal that is Ctrl+Z or
    /// Ctrl+D, and the redirected path never reaches here at all.
    /// </remarks>
    private static bool ReadInstruction(StopRules rules, ref int pending)
    {
        while (true)
        {
            Console.Error.Write("> ");
            string? typed = Console.ReadLine();

            if (typed is null)
            {
                return true;
            }

            string trimmed = typed.Trim();

            if (trimmed.Equals("q", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (trimmed.Equals("h", StringComparison.OrdinalIgnoreCase))
            {
                WriteHelp(rules);
                continue;
            }

            if (trimmed.Equals("r", StringComparison.OrdinalIgnoreCase))
            {
                pending = int.MaxValue;
                return false;
            }

            // Anything unrecognised advances one step. That is deliberate rather than an
            // oversight: at a prompt whose commonest answer is "go on", a typo should cost one
            // refinement and not a lecture.
            pending = int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int many) && many > 0
                ? many
                : 1;
            return false;
        }
    }

    /// <summary>What each key does. The single source for both the header line and the help.</summary>
    /// <remarks>
    /// One table rather than a legend in the header and a list in the help. Two renderings of
    /// one fact is what <c>../AGENTS.md</c> section Writing code calls the same rule in two
    /// places, and a key added to one and not the other is exactly how it goes wrong.
    /// </remarks>
    private static readonly (string Key, string Meaning)[] Keys =
    [
        ("Enter", "one step"),
        ("<n>", "that many steps"),
        ("r", "run to a stop rule"),
        ("h", "help"),
        ("q", "quit"),
    ];

    /// <summary>What each column of the walk means.</summary>
    private static readonly (string Column, string Meaning)[] Columns =
    [
        ("step", "zero-based index into Refinements()"),
        ("value", "the enclosure's centre, truncated to 40 decimal places"),
        ("claimed", "MaxError - the proven bound this enclosure carries, not its error"),
        ("realised", "|value - oracle| widened by the oracle's own half-width"),
        ("realised/claimed", "upper bound on how much of the claimed bound the error uses"),
        ("digits", "-log10(claimed): decimal places the bound guarantees"),
        ("gained", "digits won by this step alone"),
        ("den_bits", "bit-length of the larger denominator the enclosure carries"),
        ("ms", "wall-clock to compute this refinement, rendering excluded"),
    ];

    /// <summary>Writes the help screen to standard error, so redirected data stays clean.</summary>
    /// <param name="rules">The stop rules in force.</param>
    private static void WriteHelp(StopRules rules)
    {
        int keyWidth = Keys.Max(k => k.Key.Length);
        int columnWidth = Columns.Max(c => c.Column.Length);

        Console.Error.WriteLine();
        Console.Error.WriteLine("  keys");
        foreach ((string key, string meaning) in Keys)
        {
            Console.Error.WriteLine($"    {key.PadRight(keyWidth)}  {meaning}");
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine("  columns");
        foreach ((string column, string meaning) in Columns)
        {
            Console.Error.WriteLine($"    {column.PadRight(columnWidth)}  {meaning}");
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  stop rules: {rules.MaxSteps} steps, {rules.MaxSeconds:F0} s computing, " +
            $"{rules.MaxDenominatorBits} denominator bits"));
        Console.Error.WriteLine("  the pause does not count toward the time rule, and h costs no step");
        Console.Error.WriteLine();
    }
}
