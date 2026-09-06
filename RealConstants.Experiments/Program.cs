using System.Globalization;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// Entry point for the RealConstants bench's experiments.
/// </summary>
/// <remarks>
/// Run one by name, or list what is available:
/// <code>
/// dotnet run --project RealConstants.Experiments -- list
/// dotnet run --project RealConstants.Experiments -- compare &gt; compare.csv
/// dotnet run --project RealConstants.Experiments -- step zeta:6 euler
/// </code>
/// Nothing here has a pass or a fail, so the exit code says only whether the named experiment was
/// found and ran to completion. <c>../AGENTS.md</c> § Exactness discipline is explicit that a
/// long run printing a table must not masquerade as a test, and these depend on wall-clock time,
/// which a test may not.
/// </remarks>
internal static class Program
{
    private static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0 || args[0] == "list" || args[0] == "--help")
        {
            Usage();
            return args.Length > 0 ? 0 : 2;
        }

        if (string.Equals(args[0], "compare", StringComparison.OrdinalIgnoreCase))
        {
            return args.Length switch
            {
                1 => MethodComparison.Compare(null),
                2 => MethodComparison.Compare(args[1]),
                _ => Misuse("usage: compare [<constant>], e.g. compare zeta:2"),
            };
        }

        if (string.Equals(args[0], "step", StringComparison.OrdinalIgnoreCase))
        {
            return args.Length == 3
                ? InteractiveWalk.Walk(args[1], args[2])
                : Misuse("usage: step <constant> <method>, e.g. step zeta:6 euler");
        }

        return Misuse($"no experiment named '{args[0]}' - try 'list'.");
    }

    /// <summary>Reports a usage problem and returns the exit code the runner uses for one.</summary>
    /// <param name="message">What was wrong.</param>
    /// <returns>2.</returns>
    private static int Misuse(string message)
    {
        Console.Error.WriteLine(message);
        return 2;
    }

    private static void Usage()
    {
        Console.Error.WriteLine("RealConstants experiments - runs whose answers are not known in advance,");
        Console.Error.WriteLine("or whose cost is the thing being measured. No pass, no fail.");
        Console.Error.WriteLine("Data goes to stdout; labels and progress go to stderr.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("usage: dotnet run --project RealConstants.Experiments -- <name> [args]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  compare [<constant>]      every method for a constant, walked to a set of");
        Console.Error.WriteLine("                            error targets; steps, seconds, bound, stop rule.");
        Console.Error.WriteLine("                            With no constant, the whole bench set.");
        Console.Error.WriteLine("  step <constant> <method>  walk one method one step at a time");
        Console.Error.WriteLine();
        Console.Error.WriteLine("a constant is written <name> or <name>:<parameter>");
        Console.Error.WriteLine();

        foreach (ConstantNote note in Catalogue.Constants)
        {
            string spelling = note.Parameter.Length == 0 ? note.Name : note.Name + ":<n>";

            Console.Error.WriteLine(
                $"  {spelling,-11}  {(note.Parameter.Length == 0 ? "no parameter" : note.Parameter)}");

            foreach (Recipe recipe in Catalogue.For(note.Name))
            {
                Console.Error.WriteLine($"      {recipe.Method,-8}  {recipe.Summary}   [{recipe.Provider}]");
                Console.Error.WriteLine($"                accepts    {recipe.Domain}");
                Console.Error.WriteLine($"                identity   {recipe.Identity}");
                Console.Error.WriteLine($"                step       {recipe.StepMeaning}");
                Console.Error.WriteLine($"                cadence    {recipe.Cadence}");
            }

            Console.Error.WriteLine();
        }

        Console.Error.WriteLine("what the constants are for, per SPEC-rational-ratio.md section 4");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  pi                cross-check pair - leibniz and machin share no code, so");
        Console.Error.WriteLine("                    their enclosures overlapping is evidence about both");
        Console.Error.WriteLine("  sqrt:2, sqrt:3    the negative control - two irrationals whose ratio is");
        Console.Error.WriteLine("                    irrational, so every row of a trend matrix must plateau");
        Console.Error.WriteLine("  zeta:2, :4, :6    positive controls - pi^2/zeta(2) = 6, pi^4/zeta(4) = 90,");
        Console.Error.WriteLine("                    pi^6/zeta(6) = 945. No method here reaches zeta(s)");
        Console.Error.WriteLine("                    through pi, which is what stops those being tautologies");
        Console.Error.WriteLine("  zeta:3            the target, and the second cross-check pair");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  examples:  step pi machin     step zeta:3 borwein     step sqrt:2 newton");
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"             compare zeta:2     compare     ({Catalogue.Recipes.Length} pairings in all)"));
    }
}
