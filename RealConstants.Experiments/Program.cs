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
/// dotnet run --project RealConstants.Experiments -- step euler 6
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
            Console.Error.WriteLine("running compare ...");
            MethodComparison.Compare();
            return 0;
        }

        if (string.Equals(args[0], "step", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 3 ||
                !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int order))
            {
                Console.Error.WriteLine("usage: step <method> <s>, e.g. step euler 6");
                return 2;
            }

            return InteractiveWalk.Walk(args[1], order);
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
        Console.Error.WriteLine("usage: dotnet run --project RealConstants.Experiments -- <name> [args]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  compare            every zeta method to a set of error targets, at every");
        Console.Error.WriteLine("                     order it reaches; steps, seconds, bound and stop rule");
        Console.Error.WriteLine("  step <method> <s>  walk one method one step at a time");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  methods: " + string.Join(", ", Methods.Names));
        Console.Error.WriteLine("  orders:  central 2-4, euler 2+, direct 2+");
    }
}
