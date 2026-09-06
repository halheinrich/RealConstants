using System.Globalization;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>A constant this bench can compute, and how it is named on the command line.</summary>
/// <param name="Name">The command-line name, e.g. <c>zeta</c>.</param>
/// <param name="Title">How it reads in prose, with <c>{0}</c> where the parameter goes.</param>
/// <param name="Parameter">What the parameter means, or empty when it takes none.</param>
/// <param name="OracleMethod">The method used to measure realised error for the others.</param>
/// <param name="OracleDepth">How deep to take that oracle.</param>
/// <param name="SelfOracleDepth">
/// How deep to take it when the walk is stepping the oracle's own method, so the oracle is at
/// least far ahead of what is being measured.
/// </param>
internal sealed record ConstantNote(
    string Name,
    string Title,
    string Parameter,
    string OracleMethod,
    int OracleDepth,
    int SelfOracleDepth);

/// <summary>One way of computing one constant: a provider, and what a reader needs to know about it.</summary>
/// <param name="Constant">The constant's command-line name.</param>
/// <param name="Method">The method's command-line name.</param>
/// <param name="Provider">The implementing type, for anyone wanting the proof.</param>
/// <param name="Summary">A phrase naming the scheme.</param>
/// <param name="Identity">The identity or iteration it computes.</param>
/// <param name="StepMeaning">What the step index counts.</param>
/// <param name="Cadence">Roughly what a step buys.</param>
/// <param name="Domain">Which parameters it accepts, in prose. The provider is the authority.</param>
/// <param name="Create">Builds the provider, throwing if the parameter is outside its domain.</param>
internal sealed record Recipe(
    string Constant,
    string Method,
    string Provider,
    string Summary,
    string Identity,
    string StepMeaning,
    string Cadence,
    string Domain,
    Func<int, IRealConstant> Create);

/// <summary>
/// Every constant this bench can compute and every method available for it, in one table.
/// </summary>
/// <remarks>
/// <para>
/// <b>One table, read by everything.</b> <c>list</c>, <c>step</c> and <c>compare</c> all resolve
/// through <see cref="Recipes"/>; none of them carries its own idea of what exists. Adding a
/// provider to the bench is one row here and no other edit.
/// </para>
/// <para>
/// <b>Domain rules are not copied into this table.</b> Which radicands
/// <see cref="NewtonSquareRoot"/> accepts, and which orders <see cref="CentralBinomialZeta"/>
/// has members for, are facts each provider already owns and validates - so
/// <see cref="TryCreate"/> asks by constructing and catching rather than restating the rule
/// here. The <c>Domain</c> field is prose for a reader; the provider is the authority, and if
/// the two ever disagree the provider wins and the prose is the bug.
/// </para>
/// <para>
/// <b>One exception, and it is structural.</b> <see cref="BorweinZetaThree"/> fixes its order in
/// its type name rather than taking a parameter, so it cannot refuse <c>s = 5</c> on its own.
/// That single rule is enforced in its row below, where it is visible, rather than being left to
/// produce a zeta(3) enclosure under a zeta(5) heading.
/// </para>
/// <para>
/// <b>This file is the only one that names a concrete provider.</b> Everything downstream holds
/// <see cref="IRealConstant"/>, which is what keeps a comparison a comparison: a table that
/// reached into one provider for a figure it could not get from another would be measuring the
/// reaching. No provider gained a hook and <see cref="IRealConstant"/> did not change to make
/// any of this possible.
/// </para>
/// </remarks>
internal static class Catalogue
{
    /// <summary>The constants, in listing order.</summary>
    public static ConstantNote[] Constants { get; } =
    [
        new("pi", "pi", string.Empty, "machin", 60, 120),
        new("sqrt", "the square root of {0}", "the radicand", "newton", 10, 14),
        new("zeta", "zeta({0})", "the order s", "euler", 40, 60),
    ];

    /// <summary>Every (constant, method) pair the bench can run.</summary>
    public static Recipe[] Recipes { get; } =
    [
        new("pi", "leibniz", nameof(LeibnizPi),
            "the Gregory-Leibniz series",
            "pi/4 = 1 - 1/3 + 1/5 - 1/7 + ...",
            "step n is the partial sum over k = 0..n",
            "one decimal digit per tenfold increase in steps - a control, not a workhorse",
            "no parameter",
            _ => new LeibnizPi()),

        new("pi", "machin", nameof(MachinPi),
            "Machin's formula",
            "pi/4 = 4*arctan(1/5) - arctan(1/239), each arctangent from its Maclaurin series",
            "step n takes terms k = 0..n of both series",
            "about 1.4 decimal digits per step",
            "no parameter",
            _ => new MachinPi()),

        new("sqrt", "newton", nameof(NewtonSquareRoot),
            "Newton's method from above",
            "x_(n+1) = (x_n + c/x_n)/2, from x_0 = ceiling(sqrt(c))",
            "step n is the iterate x_n",
            "quadratic - each step roughly squares the accuracy",
            "any non-square integer of at least 2",
            radicand => new NewtonSquareRoot(radicand)),

        new("zeta", "central", nameof(CentralBinomialZeta),
            "the central-binomial series",
            "zeta(s) = c_s * sum over k >= 1 of (+/-) 1/(k^s * C(2k,k)), c_s = 3, 5/2, 36/17",
            "step n is the partial sum over k = 1..n+1",
            "about 0.6 decimal digits per step",
            "s = 2, 3 or 4",
            order => new CentralBinomialZeta(order)),

        new("zeta", "euler", nameof(EulerMaclaurinZeta),
            "Euler-Maclaurin summation",
            "zeta(s) = sum over k < N of k^-s + N^(1-s)/(s-1) + N^-s/2 "
            + "+ sum over j of (B_2j/(2j)!) * (s)_(2j-1) * N^(1-s-2j)",
            "step n uses N = n+2 exact terms, the correction count chosen to minimise the bound",
            "about 2.75 decimal digits per step",
            "s >= 2",
            order => new EulerMaclaurinZeta(order)),

        new("zeta", "borwein", nameof(BorweinZetaThree),
            "Chebyshev acceleration of the alternating zeta",
            "zeta(3) = (4/3)*eta(3), eta(3) recombined with the weights of T_m(2x-1)",
            "step n uses the Chebyshev polynomial of degree m = n+1",
            "about 0.77 decimal digits per step",
            "s = 3 only",
            order => order == 3
                ? new BorweinZetaThree()
                : throw new ArgumentOutOfRangeException(
                    nameof(order), order, "This provider computes zeta(3) and no other order.")),

        new("zeta", "direct", nameof(DirectSumZeta),
            "direct summation of the definition",
            "zeta(s) = sum over k >= 1 of 1/k^s, tail bracketed by "
            + "(N+1)^(1-s)/(s-1) < tail < N^(1-s)/(s-1)",
            "step n is the partial sum over k = 1..n+1",
            "the bound falls like N^-s, so a decimal digit costs a factor of 10^(1/s) in N "
            + "- about 3.2x at s = 2, 1.5x at s = 6",
            "s >= 2",
            order => new DirectSumZeta(order)),
    ];

    /// <summary>
    /// The set <c>compare</c> walks when given no target: every constant the bench exists to
    /// serve.
    /// </summary>
    /// <remarks>
    /// pi and the two square roots are <c>SPEC-rational-ratio.md</c> section 4's cross-check pair
    /// and negative control; the four zeta orders are its target and its positive controls.
    /// </remarks>
    public static (string Constant, int Parameter)[] BenchSet { get; } =
    [
        ("pi", 0),
        ("sqrt", 2),
        ("sqrt", 3),
        ("zeta", 2),
        ("zeta", 3),
        ("zeta", 4),
        ("zeta", 6),
    ];

    /// <summary>Finds a constant by command-line name.</summary>
    /// <param name="constant">The name.</param>
    /// <returns>The entry, or <see langword="null"/> if unknown.</returns>
    public static ConstantNote? FindConstant(string constant) =>
        Array.Find(Constants, c => string.Equals(c.Name, constant, StringComparison.OrdinalIgnoreCase));

    /// <summary>Finds a recipe by constant and method.</summary>
    /// <param name="constant">The constant's name.</param>
    /// <param name="method">The method's name.</param>
    /// <returns>The recipe, or <see langword="null"/> if that pairing does not exist.</returns>
    public static Recipe? Find(string constant, string method) =>
        Array.Find(Recipes, r =>
            string.Equals(r.Constant, constant, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.Method, method, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every method available for a constant, in listing order.</summary>
    /// <param name="constant">The constant's name.</param>
    /// <returns>The recipes.</returns>
    public static Recipe[] For(string constant) =>
        Array.FindAll(Recipes, r => string.Equals(r.Constant, constant, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Builds a provider, returning <see langword="null"/> when the provider itself refuses the
    /// parameter.
    /// </summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="parameter">The parameter, or zero when the constant takes none.</param>
    /// <returns>The provider, or <see langword="null"/> if it has no member there.</returns>
    /// <remarks>
    /// The refusal is the provider's, not this table's: a radicand that is a perfect square or an
    /// order the central-binomial family has no member for is a fact about the mathematics, and
    /// each provider already carries it in a guard with the reasoning attached. Asking by
    /// construction keeps that single-sourced.
    /// </remarks>
    public static IRealConstant? TryCreate(Recipe recipe, int parameter)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        try
        {
            return recipe.Create(parameter);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Renders a constant with its parameter, for a heading.</summary>
    /// <param name="constant">The constant's name.</param>
    /// <param name="parameter">The parameter, or zero when it takes none.</param>
    /// <returns>Prose such as <c>zeta(3)</c> or <c>the square root of 2</c>.</returns>
    public static string Title(string constant, int parameter)
    {
        ConstantNote? note = FindConstant(constant);
        return note is null
            ? constant
            : string.Format(CultureInfo.InvariantCulture, note.Title, parameter);
    }

    /// <summary>How a constant is written on the command line.</summary>
    /// <param name="constant">The constant's name.</param>
    /// <param name="parameter">The parameter, or zero when it takes none.</param>
    /// <returns><c>pi</c>, or <c>zeta:3</c>.</returns>
    public static string Spell(string constant, int parameter)
    {
        ConstantNote? note = FindConstant(constant);
        return note is null || note.Parameter.Length == 0
            ? constant
            : string.Create(CultureInfo.InvariantCulture, $"{constant}:{parameter}");
    }

    /// <summary>
    /// Parses a target such as <c>pi</c>, <c>zeta:3</c> or <c>sqrt:2</c>.
    /// </summary>
    /// <param name="text">The text as typed.</param>
    /// <param name="constant">Set to the constant's name.</param>
    /// <param name="parameter">Set to the parameter, or zero when the constant takes none.</param>
    /// <param name="problem">Set to what is wrong when parsing fails.</param>
    /// <returns><see langword="true"/> if the text names a constant this bench knows.</returns>
    /// <remarks>
    /// One shape for every constant - <c>name</c> or <c>name:parameter</c> - so the command line
    /// has uniform arity whether or not a constant is parameterised. A parameter supplied to a
    /// constant that takes none is an error rather than something quietly ignored.
    /// </remarks>
    public static bool TryParseTarget(
        string text, out string constant, out int parameter, out string problem)
    {
        constant = string.Empty;
        parameter = 0;
        problem = string.Empty;

        string[] parts = (text ?? string.Empty).Split(':');
        if (parts.Length > 2)
        {
            problem = "a target is <constant> or <constant>:<parameter>";
            return false;
        }

        ConstantNote? note = FindConstant(parts[0]);
        if (note is null)
        {
            problem = $"no constant named '{parts[0]}'";
            return false;
        }

        constant = note.Name;

        if (note.Parameter.Length == 0)
        {
            if (parts.Length == 2)
            {
                problem = $"{note.Name} takes no parameter";
                return false;
            }

            return true;
        }

        if (parts.Length != 2)
        {
            problem = $"{note.Name} needs a parameter - {note.Parameter}, as in {note.Name}:2";
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out parameter))
        {
            problem = $"'{parts[1]}' is not an integer";
            return false;
        }

        return true;
    }

    /// <summary>
    /// An enclosure far finer than the walk will reach, for reporting realised error.
    /// </summary>
    /// <param name="constant">The constant's name.</param>
    /// <param name="parameter">The parameter, or zero.</param>
    /// <param name="method">The method being measured, which the oracle avoids where it can.</param>
    /// <returns>The oracle enclosure and a phrase naming what it is.</returns>
    /// <remarks>
    /// <para>
    /// Where a second method exists for this constant the oracle is that other method, so
    /// realised error is measured against something sharing no code with what is being measured.
    /// Where the walk is stepping the oracle's own method - or where the constant has only one
    /// method, as the square roots do - it falls back to the same method taken far deeper, and
    /// says so.
    /// </para>
    /// <para>
    /// That fallback is honest for an experiment and would not be for a test. Here the column
    /// reports how fast a method converges, not whether it converges to the right number; a test
    /// asserting correctness against a deeper run of itself would be asserting nothing.
    /// </para>
    /// </remarks>
    public static (Approximation Enclosure, string Description) Oracle(
        string constant, int parameter, string method)
    {
        ConstantNote note = FindConstant(constant)
            ?? throw new ArgumentOutOfRangeException(nameof(constant), constant, "Unknown constant.");

        bool self = string.Equals(note.OracleMethod, method, StringComparison.OrdinalIgnoreCase);
        int depth = self ? note.SelfOracleDepth : note.OracleDepth;

        Recipe recipe = Find(constant, note.OracleMethod)
            ?? throw new InvalidOperationException($"No oracle recipe for {constant}.");

        IRealConstant oracle = TryCreate(recipe, parameter)
            ?? throw new InvalidOperationException($"The oracle refused {Spell(constant, parameter)}.");

        string description = string.Create(CultureInfo.InvariantCulture,
            $"{recipe.Method} at step {depth}");

        if (self)
        {
            // Two different reasons land here and they are not interchangeable. A constant with
            // one method has no alternative at all; a constant with several has one that is
            // simply too coarse to measure against - Leibniz would need 10^80 terms to resolve a
            // deep Machin step. Saying "nothing else computes this" in the second case is false,
            // and it is the kind of false that sounds like a limitation of the bench rather than
            // of the method.
            description += For(constant).Length == 1
                ? " - the SAME method, deeper, since nothing else here computes this constant"
                : " - the SAME method, deeper, the alternatives being too coarse to measure against";
        }

        return (oracle.Refinements().Skip(depth).First(), description);
    }
}
