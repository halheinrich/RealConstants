using System.Globalization;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// The batch table: every zeta method walked to a set of error targets, at every order it
/// reaches.
/// </summary>
/// <remarks>
/// This has no pass and no fail. It records steps, wall-clock time, the bound attained and which
/// stop rule fired, and a cell that stopped says what was observed rather than what the method
/// is. Direct summation stops in most cells; that is the expected result and the reason it is
/// in the table.
/// </remarks>
internal static class MethodComparison
{
    private static readonly int[] Orders = [2, 4, 6];

    private static readonly int[] TargetPlaces = [5, 10, 20, 30];

    public static void Compare()
    {
        StopRules rules = StopRules.Default;

        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"stop rules: {rules.MaxSteps} steps, {rules.MaxSeconds:F0} s, {rules.MaxDenominatorBits} denominator bits, per cell"));
        Console.Error.WriteLine();

        Console.WriteLine("order,method,target,steps,seconds,bound,denominator_bits,outcome");

        foreach (int order in Orders)
        {
            foreach (string method in Methods.Names)
            {
                IRealConstant? constant = Methods.TryCreate(method, order);
                if (constant is null)
                {
                    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"{order},{method},-,-,-,-,-,no member of this family at s={order}"));
                    continue;
                }

                foreach (int places in TargetPlaces)
                {
                    WalkResult result = Runner.WalkTo(constant, Runner.TenToTheMinus(places), rules);

                    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"{order},{method},1e-{places},{result.Steps},{result.Elapsed.TotalSeconds:F3}," +
                        $"{Presentation.Magnitude(result.Reached.MaxError)}," +
                        $"{Runner.DenominatorBits(result.Reached)},{result.Describe(rules)}"));

                    // Once a method has stopped short of a target, every harder target stops too.
                    // Recording the first is the measurement; grinding through the rest is not.
                    if (result.Reason != StopReason.TargetMet)
                    {
                        foreach (int harder in TargetPlaces)
                        {
                            if (harder > places)
                            {
                                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                                    $"{order},{method},1e-{harder},-,-,-,-,not attempted; 1e-{places} already stopped"));
                            }
                        }

                        break;
                    }
                }
            }
        }
    }
}
