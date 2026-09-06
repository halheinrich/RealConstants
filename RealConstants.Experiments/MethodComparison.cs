using System.Globalization;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// The batch table: every method for a constant, walked to a set of error targets.
/// </summary>
/// <remarks>
/// This has no pass and no fail. It records steps, wall-clock time, the bound attained and which
/// stop rule fired, and a cell that stopped says what was observed rather than what the method
/// is. Direct summation and the Gregory-Leibniz series stop in most cells; that is the expected
/// result and the reason both are in the table.
/// </remarks>
internal static class MethodComparison
{
    private static readonly int[] TargetPlaces = [5, 10, 20, 30];

    /// <summary>Decimal places for the ratio column, wide enough for a loose bound to register.</summary>
    private const int RatioPlaces = 6;

    /// <summary>Runs the comparison over the selected methods, or over the whole bench set.</summary>
    /// <param name="selectors">Zero or more selectors; none means the bench set.</param>
    /// <returns>Zero on a completed run, two on a selector that does not parse.</returns>
    public static int Compare(string[] selectors)
    {
        ArgumentNullException.ThrowIfNull(selectors);

        Choice[] choices;

        if (selectors.Length == 0)
        {
            // The bench set expands through the same grammar everything else uses, rather than
            // being iterated by a second code path that could drift from it.
            if (!Selector.TryParseAll(
                    Catalogue.BenchSet.Select(b => Selector.Spell(b.Constant, b.Parameter)),
                    out choices,
                    out string benchError))
            {
                Console.Error.WriteLine($"{benchError} - the bench set is malformed, which is a bug here.");
                return 2;
            }
        }
        else if (!Recovery.TryResolve(selectors, null, out choices))
        {
            return 2;
        }

        StopRules rules = StopRules.Default;

        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"stop rules: {rules.MaxSteps} steps, {rules.MaxSeconds:F0} s, {rules.MaxDenominatorBits} denominator bits, per cell"));
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{choices.Length} pairings selected"));
        Console.Error.WriteLine();

        Console.WriteLine("constant,method,target,steps,seconds,bound_log10,realised_log10,ratio,denominator_bits,outcome");

        foreach (Choice choice in choices)
        {
            string spelling = Selector.Spell(choice.Constant, choice.Parameter);
            IRealConstant? constant = Catalogue.TryCreate(choice.Recipe, choice.Parameter, out string refusal);

            if (constant is null)
            {
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{spelling},{choice.Recipe.Method},-,-,-,-,-,-,-,{Field(refusal)}"));
                continue;
            }

            Console.Error.WriteLine($"  {Selector.Spell(choice)} ...");

            // Built once per pairing rather than per target. The oracle avoids the method being
            // measured wherever the constant has a second one, exactly as the stepper's does.
            (Approximation oracle, _) =
                Catalogue.Oracle(choice.Constant, choice.Parameter, choice.Recipe.Method);

            RunOne(spelling, choice.Recipe, constant, oracle, rules);
        }

        return 0;
    }

    /// <summary>The realised error and its share of the claimed bound, as two CSV fields.</summary>
    /// <param name="reached">The enclosure the walk stopped on.</param>
    /// <param name="oracle">A far finer enclosure of the same constant.</param>
    /// <returns><c>realised_log10,ratio</c>, or two fields saying the oracle cannot resolve it.</returns>
    /// <remarks>
    /// <para>
    /// realised over claimed has been the most informative figure this bench produces: it is how
    /// Borwein's slack - down to 0.009 by step 39 - and the central binomial's tightness at 0.92
    /// became visible at all. The stepper has had it from the start; the file did not, so the one
    /// artefact anybody would analyse was missing the column worth analysing.
    /// </para>
    /// <para>
    /// A cell stopped by a stop rule still reports it. The walk reached a real enclosure at its
    /// stopping step, and how tight that enclosure was is a fact about the method independent of
    /// whether the target was met.
    /// </para>
    /// <para>
    /// Where the walk is finer than the oracle the two fields say so instead. From there
    /// |value - oracle| settles at the oracle's own half-width and the ratio climbs past one,
    /// which reads exactly like a violated bound - a number that cannot be supported, in the
    /// column a reader trusts most.
    /// </para>
    /// </remarks>
    private static string Realised(Approximation reached, Approximation oracle)
    {
        if (reached.MaxError <= oracle.MaxError)
        {
            return "past oracle,past oracle";
        }

        BigRational realised = BigRational.Abs(reached.Value - oracle.Value) + oracle.MaxError;

        return string.Create(CultureInfo.InvariantCulture,
            $"{Presentation.Exponent(realised)},{Presentation.Ratio(realised, reached.MaxError, RatioPlaces)}");
    }

    /// <summary>Quotes a field for CSV when it could otherwise be mistaken for two.</summary>
    /// <param name="text">The field's text.</param>
    /// <returns>The field, quoted and escaped if it holds a comma or a quote.</returns>
    /// <remarks>
    /// The outcome column is the only free text here, and it carries a provider's domain
    /// verbatim - "s = 2, 3 or 4" - which split a row into nine fields where the header declares
    /// eight. Quoting the field is the fix rather than rewording the domain, because the domain
    /// belongs to the catalogue and should not be bent to suit a downstream format.
    /// </remarks>
    private static string Field(string text) =>
        text.Contains(',', StringComparison.Ordinal) || text.Contains('"', StringComparison.Ordinal)
            ? "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : text;

    /// <summary>Walks one provider to each target in turn, stopping the row at the first failure.</summary>
    /// <param name="spelling">How the constant is written.</param>
    /// <param name="recipe">The method.</param>
    /// <param name="constant">The provider.</param>
    /// <param name="oracle">A far finer enclosure, for the realised error.</param>
    /// <param name="rules">The stop rules.</param>
    private static void RunOne(
        string spelling, Recipe recipe, IRealConstant constant, Approximation oracle, StopRules rules)
    {
        foreach (int places in TargetPlaces)
        {
            WalkResult result = Runner.WalkTo(constant, Runner.TenToTheMinus(places), rules);

            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{spelling},{recipe.Method},1e-{places},{result.Steps},{result.Elapsed.TotalSeconds:F3}," +
                $"{Presentation.Exponent(result.Reached.MaxError)}," +
                $"{Realised(result.Reached, oracle)}," +
                $"{Runner.DenominatorBits(result.Reached)},{Field(result.Describe(rules))}"));

            if (result.Reason == StopReason.TargetMet)
            {
                continue;
            }

            // Once a method has stopped short of a target, every harder target stops too.
            // Recording the first is the measurement; grinding through the rest is not.
            foreach (int harder in TargetPlaces)
            {
                if (harder > places)
                {
                    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"{spelling},{recipe.Method},1e-{harder},-,-,-,-,-,-,not attempted; 1e-{places} already stopped"));
                }
            }

            return;
        }
    }
}
