using System.Globalization;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// Entry point for the RealConstants bench's experiments.
/// </summary>
/// <remarks>
/// <para>
/// Run one by name, or list what is available:
/// </para>
/// <code>
/// dotnet run --project RealConstants.Experiments -- list
/// dotnet run --project RealConstants.Experiments -- compare &gt; compare.csv
/// dotnet run --project RealConstants.Experiments -- compare zeta:3/central zeta:3/euler
/// dotnet run --project RealConstants.Experiments -- step zeta:3/central
/// </code>
/// <para>
/// Both experiments take zero or more selectors and neither parses them itself:
/// <see cref="Selector"/> owns the grammar, so "which method" has one spelling rather than one
/// per command. This entry point identifies the command and hands the rest of the line over.
/// </para>
/// <para>
/// Nothing here has a pass or a fail, so the exit code says only whether the named experiment was
/// found and ran to completion. <c>../AGENTS.md</c> § Exactness discipline is explicit that a
/// long run printing a table must not masquerade as a test, and these depend on wall-clock time,
/// which a test may not.
/// </para>
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

        string[] selectors = args[1..];

        if (string.Equals(args[0], "compare", StringComparison.OrdinalIgnoreCase))
        {
            return MethodComparison.Compare(selectors);
        }

        if (string.Equals(args[0], "step", StringComparison.OrdinalIgnoreCase))
        {
            return InteractiveWalk.Walk(selectors);
        }

        Console.Error.WriteLine($"no experiment named '{args[0]}' - try 'list'.");
        return 2;
    }

    private static void Usage()
    {
        Console.Error.WriteLine("RealConstants experiments - runs whose answers are not known in advance,");
        Console.Error.WriteLine("or whose cost is the thing being measured. No pass, no fail.");
        Console.Error.WriteLine("Data goes to stdout; labels and progress go to stderr.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("usage: dotnet run --project RealConstants.Experiments -- <name> [selector ...]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  compare [selector ...]  every selected method walked to a set of error");
        Console.Error.WriteLine("                          targets; steps, seconds, bound, stop rule.");
        Console.Error.WriteLine("                          With no selector, the whole bench set.");
        Console.Error.WriteLine("  step <selector>         walk one method one step at a time. The selection");
        Console.Error.WriteLine("                          must come to exactly one method.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("selectors");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  a selector is  <constant>[:<parameter>][/<method>]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  omit /<method> and it names every method for that constant. Both commands");
        Console.Error.WriteLine("  take zero or more, so a subset is written as several:");
        Console.Error.WriteLine();
        Console.Error.WriteLine("    compare                              the bench set");
        Console.Error.WriteLine("    compare zeta:3                       every method for zeta(3)");
        Console.Error.WriteLine("    compare zeta:3/central zeta:3/euler  exactly those two");
        Console.Error.WriteLine("    compare pi/machin sqrt:2/newton      across constants");
        Console.Error.WriteLine("    step zeta:3/central                  the walk");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  a parameter is required where a constant has one and refused where it has");
        Console.Error.WriteLine("  none. Whether a provider accepts a given parameter is the provider's own");
        Console.Error.WriteLine("  rule, so sqrt:4/newton is a valid selector that fails when it is built,");
        Console.Error.WriteLine("  with that provider's explanation rather than a guess made here.");
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
                Console.Error.WriteLine($"                identity   {recipe.Identity(0)}");
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
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {Catalogue.Recipes.Length} pairings in all, over {Catalogue.Constants.Length} constants."));
    }
}
