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
            Console.Error.WriteLine("  Enter = one step, a number = that many steps, r = run to a stop rule, q = quit");
        }
        else
        {
            Console.Error.WriteLine("  stdin is not a terminal, so the pause is skipped and this runs to a stop rule");
        }

        Console.Error.WriteLine();
        Console.WriteLine("step | value | claimed | realised | realised/claimed | digits | gained | den_bits | ms");

        Stopwatch total = Stopwatch.StartNew();
        Stopwatch perStep = new();
        int pending = interactive ? 1 : int.MaxValue;
        double previousDigits = 0;
        int step = 0;
        StopReason reason = StopReason.StepLimit;

        foreach (Approximation refinement in constant.Refinements())
        {
            perStep.Restart();

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
                $"{perStep.Elapsed.TotalMilliseconds:F1}"));

            previousDigits = digits;
            step++;

            if (step >= rules.MaxSteps)
            {
                reason = StopReason.StepLimit;
                break;
            }

            if (total.Elapsed.TotalSeconds >= rules.MaxSeconds)
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

            Console.Error.Write("> ");
            string? typed = Console.ReadLine();

            if (typed is null || typed.Trim().Equals("q", StringComparison.OrdinalIgnoreCase))
            {
                reason = StopReason.UserQuit;
                break;
            }

            string trimmed = typed.Trim();
            if (trimmed.Equals("r", StringComparison.OrdinalIgnoreCase))
            {
                pending = int.MaxValue;
            }
            else if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int many) && many > 0)
            {
                pending = many;
            }
            else
            {
                pending = 1;
            }
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"stopped after {step} steps in {total.Elapsed.TotalSeconds:F2} s: " +
            $"{new WalkResult(step, default, total.Elapsed, reason).Describe(rules)}"));

        return 0;
    }
}
